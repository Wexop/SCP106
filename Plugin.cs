using System;
using System.Collections.Generic;
using BepInEx;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using UnityEngine;
using LethalLib.Modules;
using SCP106.Scripts;
using SCP106.Utils;

namespace SCP106
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class SCP106Plugin : BaseUnityPlugin
    {

        const string GUID = "wexop.scp_106";
        const string NAME = "SCP_106";
        const string VERSION = "1.0.0";

        public static SCP106Plugin instance;

        public GameObject dimensionObject;
        public GameObject actualDimensionObjectInstantiated;
        public SCP106DimensionManager actualDimensionObjectManager;
        
        public ConfigEntry<string> spawnMoonRarity;
        public ConfigEntry<float> dimensionPosY;

        void Awake()
        {
            instance = this;
            
            Logger.LogInfo($"SCP_106 starting....");

            string assetDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "scp106");
            AssetBundle bundle = AssetBundle.LoadFromFile(assetDir);
            
            Logger.LogInfo($"SCP_106 bundle found !");
            
            NetcodePatcher();
            LoadConfigs();
            RegisterMonster(bundle);
            LoadDimension(bundle);
            
            
            Logger.LogInfo($"SCP_106 is ready!");
        }
        
        
        public void LoadDimension(AssetBundle bundle)
        {
            GameObject dimension = bundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/SCP106/DimensionSCP106.prefab");
            dimensionObject = dimension;
            Logger.LogInfo($"{dimension.name} FOUND");
            
            Utilities.FixMixerGroups(dimension);
        }
        
        public void InstantiateDimension()
        {
            if (actualDimensionObjectInstantiated == null)
            {
                DestroyDimension();
                actualDimensionObjectInstantiated = Instantiate(dimensionObject, Vector3.up * -dimensionPosY.Value, Quaternion.identity);
                actualDimensionObjectManager = actualDimensionObjectInstantiated.GetComponent<SCP106DimensionManager>();
            }

        }
        
        public void DestroyDimension()
        {
            if (actualDimensionObjectInstantiated != null)
            {
                Destroy(actualDimensionObjectInstantiated);
                Destroy(actualDimensionObjectManager);
                actualDimensionObjectInstantiated = null;
                actualDimensionObjectManager = null;
            }
        }

        string RarityString(int rarity)
        {
            return
                $"Modded:{rarity},ExperimentationLevel:{rarity},AssuranceLevel:{rarity},VowLevel:{rarity},OffenseLevel:{rarity},MarchLevel:{rarity},RendLevel:{rarity},DineLevel:{rarity},TitanLevel:{rarity},Adamance:{rarity},Embrion:{rarity},Artifice:{rarity}";

        }

        void LoadConfigs()
        {
            
            //GENERAL
            
            spawnMoonRarity = Config.Bind("General", "SpawnRarity", 
                "Modded:40,ExperimentationLevel:20,AssuranceLevel:20,VowLevel:20,OffenseLevel:25,MarchLevel:25,RendLevel:30,DineLevel:30,TitanLevel:50,Adamance:50,Embrion:50,Artifice:55", 
                "Chance for SCP 106 to spawn for any moon, example => assurance:100,offense:50 . You need to restart the game.");
            CreateStringConfig(spawnMoonRarity, true);
            
            dimensionPosY = Config.Bind("Pocket Dimension", "dimensionPosY", 550f,
                "Dimension Y position. No need to restart the game !");
            CreateFloatConfig(dimensionPosY, 0f, 3000f);
 
        }
        
        void RegisterMonster(AssetBundle bundle)
        {
            //creature
            EnemyType creature = bundle.LoadAsset<EnemyType>("Assets/LethalCompany/Mods/SCP106/SCP106.asset");

            creature.MaxCount = 1;
            
            Logger.LogInfo($"{creature.name} FOUND");
            Logger.LogInfo($"{creature.enemyPrefab} prefab");
            NetworkPrefabs.RegisterNetworkPrefab(creature.enemyPrefab);
            Utilities.FixMixerGroups(creature.enemyPrefab);

            TerminalNode terminalNodeBigEyes = new TerminalNode();
            terminalNodeBigEyes.creatureName = "SCP106";
            terminalNodeBigEyes.displayText = "";

            TerminalKeyword terminalKeywordBigEyes = new TerminalKeyword();
            terminalKeywordBigEyes.word = "SCP106";
            
            
            RegisterUtil.RegisterEnemyWithConfig(spawnMoonRarity.Value, creature,terminalNodeBigEyes , terminalKeywordBigEyes, creature.PowerLevel, creature.MaxCount);

        }
        
        /// <summary>
        ///     Slightly modified version of: https://github.com/EvaisaDev/UnityNetcodePatcher?tab=readme-ov-file#preparing-mods-for-patching
        /// </summary>
        private static void NetcodePatcher()
        {
            Type[] types;
            try
            {
                types = Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                // This goofy try catch is needed here to be able to use soft dependencies in the future, though none are present at the moment.
                types = e.Types.Where(type => type != null).ToArray();
            }

            foreach (Type type in types)
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length > 0)
                    {
                        // Do weird magic...
                        _ = method.Invoke(null, null);
                    }
                }
            }
        }
        private void CreateFloatConfig(ConfigEntry<float> configEntry, float min = 0f, float max = 100f)
        {
            var exampleSlider = new FloatSliderConfigItem(configEntry, new FloatSliderOptions
            {
                Min = min,
                Max = max,
                RequiresRestart = false
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }
        
        private void CreateIntConfig(ConfigEntry<int> configEntry, int min = 0, int max = 100)
        {
            var exampleSlider = new IntSliderConfigItem(configEntry, new IntSliderOptions()
            {
                Min = min,
                Max = max,
                RequiresRestart = false
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }
        
        private void CreateStringConfig(ConfigEntry<string> configEntry, bool requireRestart = false)
        {
            var exampleSlider = new TextInputFieldConfigItem(configEntry, new TextInputFieldOptions()
            {
                RequiresRestart = requireRestart
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }
        
        public bool StringContain(string name, string verifiedName)
        {
            var name1 = name.ToLower();
            while (name1.Contains(" ")) name1 = name1.Replace(" ", "");

            var name2 = verifiedName.ToLower();
            while (name2.Contains(" ")) name2 = name2.Replace(" ", "");

            return name1.Contains(name2);
        }
        
        private void CreateBoolConfig(ConfigEntry<bool> configEntry)
        {
            var exampleSlider = new BoolCheckBoxConfigItem(configEntry, new BoolCheckBoxOptions
            {
                RequiresRestart = false
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }
        
    }
}