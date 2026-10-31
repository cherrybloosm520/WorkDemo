using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Demo.Editor
{
    /// <summary>
    /// 一键搭建 Mixamo 角色管线,菜单顺序执行 1 → 2 → 3:
    ///   1. 把 Art 目录下所有 FBX 配置为人形骨骼(Mixamo 骨架可自动映射)
    ///   2. 按文件名关键字(Idle/Walk/Run/Jump/Fall/Land)生成移动动画控制器
    ///   3. 用角色 FBX 生成玩家预制体(CharacterController + PlayerCharacter + Animator + PlayerAnimator)
    /// 预期目录结构:
    ///   Assets/_Demo/Art/Character/  角色 FBX(T-Pose、带皮肤)
    ///   Assets/_Demo/Art/Animations/ 动画 FBX(In Place、不带皮肤)
    /// 所有步骤幂等,可重复执行。
    /// </summary>
    public static class PlayerRigBuilder
    {
        private const string ArtRoot = "Assets/_Demo/Art";
        private const string CharacterDir = ArtRoot + "/Character";
        private const string AnimationDir = ArtRoot + "/Animations";
        private const string ControllerPath = ArtRoot + "/PlayerLocomotion.controller";
        private const string PrefabPath = "Assets/_Demo/Resources/Player.prefab";

        /// <summary>命令行一键入口(菜单 1→2→3 的串联),供 batchmode 执行。</summary>
        public static void SetupAll()
        {
            ConfigureMixamoImports();
            BuildLocomotionController();
            BuildPlayerPrefab();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Demo/角色搭建/1. 配置 Mixamo 导入设置(人形骨骼)")]
        public static void ConfigureMixamoImports()
        {
            EnsureDirectory(CharacterDir);
            EnsureDirectory(AnimationDir);

            var count = 0;
            foreach (var fbx in Directory.GetFiles(FullPath(ArtRoot), "*.fbx", SearchOption.AllDirectories))
            {
                var importer = AssetImporter.GetAtPath(ToAssetPath(fbx)) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[PlayerRigBuilder] 无法读取导入器:{fbx}");
                    continue;
                }

                // 注意:中国版 Unity 的 ModelImporterAnimationType 枚举与标准版存在差异
                // (标准版的 Humanoid 成员在此编译报 CS0117),这里改用序列化字段 m_AnimationType,
                // 取值与 ModelImporter YAML 一致:1 = Legacy,2 = Generic,3 = Humanoid
                var serialized = new SerializedObject(importer);
                var animType = serialized.FindProperty("m_AnimationType");
                if (animType != null && animType.intValue != 3)
                {
                    animType.intValue = 3;
                    serialized.ApplyModifiedProperties();
                }
                importer.SaveAndReimport();

                // SaveAndReimport 可能异步返回,强制同步导入,确保后续能立即读到动画剪辑
                AssetDatabase.ImportAsset(ToAssetPath(fbx),
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                count++;
            }

            Debug.Log($"[PlayerRigBuilder] 已配置 {count} 个 FBX 为人形骨骼,等待导入完成。");
        }

        [MenuItem("Demo/角色搭建/2. 生成移动动画控制器")]
        public static void BuildLocomotionController()
        {
            var clips = FindClips();

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            var idle = AddState(sm, "Idle", clips["idle"]);
            var walk = AddState(sm, "Walk", clips["walk"]);
            var run = AddState(sm, "Run", clips["run"]);
            var jump = AddState(sm, "JumpStart", clips["jump"]);
            var fall = AddState(sm, "Fall", clips["fall"]);
            var land = AddState(sm, "Land", clips["land"]);

            sm.defaultState = idle;

            // 速度三档:Idle ↔ Walk ↔ Run
            Transition(idle, walk, (AnimatorConditionMode.Greater, 0.1f, "Speed"));
            Transition(walk, idle, (AnimatorConditionMode.Less, 0.1f, "Speed"));
            Transition(walk, run, (AnimatorConditionMode.Greater, 4.5f, "Speed"));
            Transition(run, walk, (AnimatorConditionMode.Less, 4.5f, "Speed"));

            // 跳跃:触发器进入起跳,顶点(垂直速度转负)后进入下落,落地播 Landing
            AnyTransition(sm, jump, (AnimatorConditionMode.If, 0f, "Jump"));
            Transition(jump, fall, (AnimatorConditionMode.Less, 0f, "VerticalVelocity"));
            Transition(fall, land, (AnimatorConditionMode.If, 0f, "Grounded"));
            ExitTransition(land, idle, 0.9f);

            // 失足坠落:离地且开始下落(与跳跃路径错开,跳跃起跳时垂直速度为正不会误入)
            AnyTransition(sm, fall,
                (AnimatorConditionMode.IfNot, 0f, "Grounded"),
                (AnimatorConditionMode.Less, 0f, "VerticalVelocity"));

            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayerRigBuilder] 动画控制器已生成:{ControllerPath}");
        }

        [MenuItem("Demo/角色搭建/3. 生成玩家预制体")]
        public static void BuildPlayerPrefab()
        {
            // 角色目录可能有多个模型(如敌人),优先选名称含 kyle/agent 的 Mixamo 角色
            var candidates = AssetDatabase.FindAssets("t:Model", new[] { CharacterDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx"))
                .ToList();
            if (candidates.Count == 0)
                throw new FileNotFoundException($"角色目录没有 FBX:{CharacterDir},请先把 Mixamo 角色放入该目录");

            var characterPath = candidates.FirstOrDefault(p =>
                                    Path.GetFileName(p).ToLowerInvariant().Contains("kyle") ||
                                    Path.GetFileName(p).ToLowerInvariant().Contains("agent"))
                                ?? candidates[0];
            var characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(characterPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                throw new FileNotFoundException($"动画控制器不存在:{ControllerPath},请先执行菜单第 2 步");

            var avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(characterAsset))
                .OfType<Avatar>().FirstOrDefault();
            if (avatar == null)
                throw new FileNotFoundException("角色 FBX 没有 Avatar,请先执行菜单第 1 步(人形骨骼配置)");

            // 结构:Player 根(控制组件)→ Model 子物体(模型与 Animator)→ LookTarget 子物体
            var root = new GameObject("Player");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(characterAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);

            // 移除模型自带的碰撞体(如有),碰撞由 CharacterController 统一负责
            foreach (var col in model.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            root.AddComponent<PlayerCharacter>();
            root.AddComponent<PlayerAnimator>();

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // 位移由代码驱动,动画均为 In Place

            var lookTarget = new GameObject("LookTarget");
            lookTarget.transform.SetParent(root.transform, false);
            lookTarget.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            EnsureDirectory("Assets/_Demo/Resources");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayerRigBuilder] 玩家预制体已生成:{PrefabPath}");
        }

        // ---------- 内部工具 ----------

        /// <summary>
        /// 按剪辑名关键字收集 6 个移动动画。
        /// 同时搜索 Animations 与 Character 目录(兼容"单 FBX 含全部动画"的资产);
        /// 跳过 *_To_* 过渡动画;fall/land 缺失时回退复用 jump,保证最小动画集也能跑通。
        /// </summary>
        private static Dictionary<string, AnimationClip> FindClips()
        {
            var result = new Dictionary<string, AnimationClip>();
            var searchDirs = new List<string>();
            if (AssetDatabase.IsValidFolder(AnimationDir)) searchDirs.Add(AnimationDir);
            if (AssetDatabase.IsValidFolder(CharacterDir)) searchDirs.Add(CharacterDir);
            if (searchDirs.Count == 0)
                throw new FileNotFoundException($"未找到动画目录 {AnimationDir} 或 {CharacterDir},请先放入 Mixamo 动画 FBX");

            var fbxPaths = searchDirs
                .SelectMany(dir => AssetDatabase.FindAssets("t:Model", new[] { dir }))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx"));

            foreach (var fbx in fbxPaths)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault();
                if (clip == null)
                    continue;

                // Mixamo 的 FBX take 名可能是 "mixamo.com",剪辑名不可靠时用文件名兜底
                var clipName = clip.name.ToLowerInvariant();
                var fileName = Path.GetFileNameWithoutExtension(fbx).ToLowerInvariant();
                if (clipName.Contains("_to_") || fileName.Contains("_to_"))
                    continue; // 过渡动画(如 Idle_To_Running)不参与匹配

                // 匹配顺序:fall/land 优先(如 "Falling Idle" 同时含 fall 和 idle)
                foreach (var key in new[] { "fall", "land", "jump", "walk", "run", "idle" })
                {
                    if (result.ContainsKey(key))
                        continue;
                    if (clipName.Contains(key) || fileName.Contains(key))
                    {
                        result[key] = clip;
                        break;
                    }
                }
            }

            foreach (var required in new[] { "idle", "walk", "run", "jump" })
            {
                if (!result.ContainsKey(required))
                    throw new FileNotFoundException($"缺少动画 {required}(按剪辑名关键字未匹配到),请检查动画目录");
            }

            // fall/land 可选:缺失时复用 jump,保证最小动画集也能跑通
            if (!result.ContainsKey("fall"))
            {
                result["fall"] = result["jump"];
                Debug.LogWarning("[PlayerRigBuilder] 未找到下落动画,回退复用跳跃动画。");
            }
            if (!result.ContainsKey("land"))
            {
                result["land"] = result["jump"];
                Debug.LogWarning("[PlayerRigBuilder] 未找到落地动画,回退复用跳跃动画。");
            }

            return result;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            var state = sm.AddState(name);
            state.motion = clip;
            state.writeDefaultValues = false;
            return state;
        }

        private static void Transition(AnimatorState from, AnimatorState to,
            params (AnimatorConditionMode mode, float threshold, string param)[] conditions)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            foreach (var (mode, threshold, param) in conditions)
                transition.AddCondition(mode, threshold, param);
        }

        private static void AnyTransition(AnimatorStateMachine sm, AnimatorState to,
            params (AnimatorConditionMode mode, float threshold, string param)[] conditions)
        {
            var transition = sm.AddAnyStateTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            foreach (var (mode, threshold, param) in conditions)
                transition.AddCondition(mode, threshold, param);
        }

        private static void ExitTransition(AnimatorState from, AnimatorState to, float exitTime)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = 0.15f;
        }

        /// <summary>递归确保资产目录存在。</summary>
        private static void EnsureDirectory(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            var parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }

        private static string FullPath(string assetPath) => Path.GetFullPath(assetPath);

        private static string ToAssetPath(string fullPath)
        {
            var assetsRoot = Path.GetFullPath("Assets");
            return "Assets" + fullPath.Substring(assetsRoot.Length).Replace('\\', '/');
        }
    }
}
