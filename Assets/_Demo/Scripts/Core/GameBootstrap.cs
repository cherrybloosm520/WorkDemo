using Demo.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Demo.Core
{
    /// <summary>
    /// 开发期引导:任意场景 Play 时,若场景中还没有玩家角色,自动搭建最小可玩环境
    /// (地面 + 玩家 + 轨道相机 + 光照)。
    /// 玩家优先使用 Resources/Player 预制体(Mixamo 角色,由菜单 Demo/角色搭建 生成);
    /// 预制体不存在时回退为开发用胶囊。正式训练场场景搭建完成后,场景内已摆放角色,此引导自动跳过。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDevRig()
        {
            if (Object.FindFirstObjectByType<PlayerCharacter>() != null)
                return;

            BuildDevRig();
        }

        private static void BuildDevRig()
        {
            // 地面:厚实心方块给 CC 稳定支撑,顶面在 y=0。
            // 尺寸取 100×100:冲刺(6.2m/s)从中心到边缘约 8 秒,开发期不易跑出边界。
            // 正式训练场场景会用真实地形替换。
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground (Dev)";
            ground.transform.position = new Vector3(0f, -1f, 0f);
            ground.transform.localScale = new Vector3(100f, 2f, 100f);
            ground.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial(new Color(0.16f, 0.17f, 0.19f));

            // 玩家(预制体优先,胶囊回退)
            var player = SpawnPlayer();
            var lookTarget = player.transform.Find("LookTarget");
            if (lookTarget == null)
            {
                var go = new GameObject("LookTarget");
                go.transform.SetParent(player.transform, false);
                go.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                lookTarget = go.transform;
            }

            // 主相机(场景已有则复用)
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            if (cam.GetComponent<Cinemachine.CinemachineBrain>() == null)
                cam.gameObject.AddComponent<Cinemachine.CinemachineBrain>();

            // 轨道相机控制器
            var cameraRig = new GameObject("PlayerCameraRig");
            var cameraController = cameraRig.AddComponent<PlayerCameraController>();
            cameraController.Bind(player.transform, lookTarget);

            // 光照(场景已有则复用)
            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sun");
                var light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        /// <summary>优先实例化 Mixamo 角色预制体;不存在则回退到开发胶囊。</summary>
        private static GameObject SpawnPlayer()
        {
            var prefab = Resources.Load<GameObject>("Player");
            if (prefab != null)
            {
                var player = Object.Instantiate(prefab);
                player.name = "Player";
                player.transform.position = new Vector3(0f, 0.05f, 0f);
                EnsurePlayerLayer(player);
                return player;
            }

            var capsule = BuildCapsulePlayer();
            EnsurePlayerLayer(capsule);
            return capsule;
        }

        /// <summary>
        /// 玩家与子物体统一放到 Player 图层:着地检测等物理查询用掩码排除自身
        /// (依赖 TagManager 中已定义的 Player 图层,由 PlayerRigBuilder 创建)。
        /// </summary>
        private static void EnsurePlayerLayer(GameObject player)
        {
            var playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer < 0)
                return;

            foreach (var t in player.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = playerLayer;
        }

        private static GameObject BuildCapsulePlayer()
        {
            var player = new GameObject("Player (Dev Capsule)");
            player.transform.position = new Vector3(0f, 0.05f, 0f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            player.AddComponent<PlayerCharacter>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body (Dev)";
            Object.Destroy(body.GetComponent<CapsuleCollider>());
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(1f, 0.9f, 1f);
            body.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial(new Color(0.32f, 0.36f, 0.44f));

            return player;
        }

        private static Material CreateLitMaterial(Color color)
        {
            return new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        }
    }
}
