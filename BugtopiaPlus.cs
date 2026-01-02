using BepInEx;
using BepInEx.Configuration; // 引入配置系统命名空间
using BepInEx.Logging;
using HarmonyLib;
using Peecub; // 游戏原本的命名空间

namespace BugtopiaPlus
{
    [BepInPlugin("BugtopiaPlus", "BugtopiaPlus", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Plugin Instance; // 单例实例，方便访问配置

        // --- 定义配置项 (Config Entries) ---
        public static ConfigEntry<float> FoodExpMultiplier;
        public static ConfigEntry<float> MateAndEggSpeedMultiplier;
        public static ConfigEntry<float> MutationRate;
        public static ConfigEntry<bool> EnableUnrestrictedFeeding;

        private void Awake()
        {
            Log = base.Logger;
            Instance = this;

            // --- 绑定配置文件 ---
            // Bind(分组名称, 配置项键名, 默认值, 描述)
            
            FoodExpMultiplier = Config.Bind("General", 
                "FoodExpMultiplier", 
                10.0f, 
                "给予虫虫食物时获得经验的倍率，默认为10倍经验。");

            MateAndEggSpeedMultiplier = Config.Bind("General", 
                "MateAndEggSpeedMultiplier", 
                10.0f, 
                "虫虫繁殖和卵在交配箱中生长时的速度倍率，默认为10倍速度。");

            MutationRate = Config.Bind("General", 
                "MutationRate", 
                0.1f, 
                "虫虫交配后获得闪光的概率，默认为0.1（10%概率）。");

            EnableUnrestrictedFeeding = Config.Bind("Toggles", 
                "EnableUnrestrictedFeeding", 
                true, 
                "为true时，你可以在虫虫为蛹阶段时喂食。");

            // 应用补丁
            Harmony.CreateAndPatchAll(typeof(UnifiedPatches));
            Log.LogInfo("BugtopiaPlus loaded successfully with Config support!");
        }
    }

    // 将所有补丁整合到一个类中，显得更整洁
    public static class UnifiedPatches
    {
        // ----------------------------------------------------
        // 1. 食物经验倍率修改
        // ----------------------------------------------------
        [HarmonyPatch(typeof(DataManager), "Awake")]
        [HarmonyPostfix]
        public static void DataManager_Awake_Postfix()
        {
            if (DataManager.instance != null)
            {
                // 读取配置文件的值
                DataManager.instance.foodExpMultiplier = Plugin.FoodExpMultiplier.Value;
                Plugin.Log.LogInfo($"[Config] Food Exp Multiplier set to: {DataManager.instance.foodExpMultiplier}");
            }
        }

        // ----------------------------------------------------
        // 2. 变异概率修改
        // ----------------------------------------------------
        [HarmonyPatch(typeof(RM), "Awake")]
        [HarmonyPostfix]
        public static void RM_Awake_Postfix()
        {
            if (RM.instance != null)
            {
                // 读取配置文件的值
                RM.instance.mutationRate = Plugin.MutationRate.Value;
                Plugin.Log.LogInfo($"[Config] Mutation Rate set to: {RM.instance.mutationRate}");
            }
        }

        // ----------------------------------------------------
        // 3. 繁殖与卵生长速率修改
        // ----------------------------------------------------
        [HarmonyPatch(typeof(IdleObject), "GetMateBoxSpeedMul")]
        [HarmonyPostfix]
        public static void GetMateBoxSpeedMul_Postfix(ref float __result)
        {
            if (__result > 0)
            {
                // 直接乘以上配置文件的倍率
                __result *= Plugin.MateAndEggSpeedMultiplier.Value;
            }
        }

        // ----------------------------------------------------
        // 4. 解除不可喂食限制 (例如喂蛹)
        // ----------------------------------------------------
        [HarmonyPatch(typeof(IdleObject), "TryFeed")]
        [HarmonyPrefix]
        public static bool TryFeed_Prefix(IdleObject __instance, ref bool __result)
        {
            // 如果配置文件中关闭了这个功能，则返回 true，执行游戏原本的逻辑
            if (!Plugin.EnableUnrestrictedFeeding.Value)
            {
                return true; 
            }

            // --- 以下是你原本的强制喂食逻辑 ---
            
            // 必要的空值和激活检查
            if (__instance.nextStageIdleObject == null || !__instance.isActive)
            {
                __result = false;
                return false; // 拦截原方法
            }

            // 增加经验
            __instance.exp += DataManager.instance.GetExpPerFeed();
            __result = true; // 标记喂食成功

            return false; // 返回 false 以拦截游戏原有的 TryFeed 方法
        }
    }
}
