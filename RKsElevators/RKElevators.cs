using System.Reflection;
using BepInEx;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using fastJSON;

namespace RKsElevators
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class RKsElevators : BaseUnityPlugin
    {
        public const string PluginGUID = "com.RockerKitten.RKsElevators";
        public const string PluginName = "RKsElevators";
        public const string PluginVersion = "1.0.0";
        
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
        private AssetBundle BuildAssetBundle { get; set; }
        private Dictionary<BuildMaterial, BuildEffectLists> effects;

        private void Awake()
        {
            AddLocalizations();
            LoadEmbeddedAssembly("fastJSON.dll");
            this.BuildAssetBundle = AssetUtils.LoadAssetBundleFromResources("rkc_elevator", Assembly.GetExecutingAssembly());
            PrefabManager.OnVanillaPrefabsAvailable += SetupAssets;
            Jotunn.Logger.LogInfo("Queueing elevator music... Elevator music initialized.");
        }

        private void SetupAssets()
        {
            this.effects = InitializeEffects();
            InitializeBuildAssets();
            PrefabManager.OnVanillaPrefabsAvailable -= SetupAssets;
        }


        private void InitializeBuildAssets()
        {
            var BuildAssets = LoadEmbeddedJsonFile<BuildAssets>("RKsElevators.json");
             foreach (var BuildPiece in BuildAssets.Pieces)
             {
                var customPiece = this.BuildCustomPiece(BuildPiece);
                // load supplemental assets (sfx and vfx)
                this.AttachEffects(customPiece.PiecePrefab, BuildPiece);
                PieceManager.Instance.AddPiece(customPiece);
             }
                
        }

        private Dictionary<BuildMaterial, BuildEffectLists> InitializeEffects()
        {
            Dictionary<string, GameObject> effectCache = new Dictionary<string, GameObject>();
            GameObject loadfx(string prefabName)
            {
                if (!effectCache.ContainsKey(prefabName))
                {
                    effectCache.Add(prefabName, PrefabManager.Cache.GetPrefab<GameObject>(prefabName));
                }
                return effectCache[prefabName];
            }
            EffectList createfxlist(params string[] effectsList) => new EffectList { m_effectPrefabs = effectsList.Select(fx => new EffectList.EffectData { m_prefab = loadfx(fx) }).ToArray() };

            var effects = new Dictionary<BuildMaterial, BuildEffectLists>
            {
                {
                    BuildMaterial.Wood,
                    new BuildEffectLists
                    {
                        Place = createfxlist("sfx_build_hammer_wood", "vfx_Place_stone_wall_2x1"),
                        Break = createfxlist("sfx_wood_break", "vfx_SawDust"),
                        Hit   = createfxlist("vfx_SawDust")
                    }
                },
                {
                    BuildMaterial.Stone,
                    new BuildEffectLists
                    {
                        Place = createfxlist("sfx_build_hammer_stone", "vfx_Place_stone_wall_2x1"),
                        Break = createfxlist("sfx_rock_destroyed", "vfx_Place_stone_wall_2x1"),
                        Hit   = createfxlist("sfx_Rock_Hit")
                    }
                },
                {
                    BuildMaterial.Metal,
                    new BuildEffectLists
                    {
                        Place = createfxlist("sfx_build_hammer_metal", "vfx_Place_stone_wall_2x1"),
                        Break = createfxlist("sfx_rock_destroyed", "vfx_HitSparks"),
                        Hit   = createfxlist("vfx_HitSparks")
                    }
                }
            };

            return effects;
        }

        private void AddLocalizations()
        {
            Localization = LocalizationManager.Instance.GetLocalization();
            Localization.AddTranslation("English", new Dictionary<String, String>
            {
                { "piece_rk_elevatortall", "Tall Elevator"},{"jotunn_cat_elevators","RKs Elevators" },{"piece_rk_elevatortall_description","Elevator that goes from 8m to ground floor. No stopping between floors!"},
                { "piece_rk_elevatorshort", "Shorter Elevator"},{ "piece_rk_elevator2", "Simple Elevator"},{"piece_rk_elevatorshort_description","Elevator that goes from 4m to ground floor. No stopping between floors!"},
                {"piece_rk_elevator2_description","Simpler elevator that goes from 4m to ground floor. No stopping between floors!"}
            });
        }

        private CustomPiece BuildCustomPiece(BuildPiece BuildPiece)
        {
            var BuildPiecePrefab = this.BuildAssetBundle.LoadAsset<GameObject>(BuildPiece.PrefabName);

            var pieceConfig = new PieceConfig
            {
                // TODO: verify token string
                Name = BuildPiece.DisplayNameToken,
                Description = BuildPiece.PrefabDescription,
                // NOTE: could move override to json config if needed.
                AllowedInDungeons = false,
                PieceTable = "_HammerPieceTable",
                Category = "$jotunn_cat_elevators",
                Enabled = BuildPiece.Enabled
            };
            if (!string.IsNullOrWhiteSpace(BuildPiece.RequiredStation))
            {
                pieceConfig.CraftingStation = BuildPiece.RequiredStation;
            }

            var requirements = BuildPiece.Requirements
                .Select(r => new RequirementConfig(r.Item, r.Amount, recover: r.Recover));

            pieceConfig.Requirements = requirements.ToArray();
            var customPiece = new CustomPiece(BuildPiecePrefab, fixReference: false, pieceConfig);
            return customPiece;
        }

        private void AttachEffects(GameObject piecePrefab, BuildPiece BuildPiece)
        {
            var pieceComponent = piecePrefab.GetComponent<Piece>();
            pieceComponent.m_placeEffect = this.effects[BuildPiece.Material].Place;

            var wearComponent = piecePrefab.GetComponent<WearNTear>();
            wearComponent.m_destroyedEffect = this.effects[BuildPiece.Material].Break;
            wearComponent.m_hitEffect = this.effects[BuildPiece.Material].Hit;
            
        }

        // LOADING EMBEDDED RESOURCES
        private void LoadEmbeddedAssembly(string assemblyName)
        {
            var stream = GetManifestResourceStream(assemblyName);
            if (stream == null)
            {
                Logger.LogError($"Could not load embedded assembly ({assemblyName})!");
                return;
            }

            using (stream)
            {
                var data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                Assembly.Load(data);
            }
        }

        private Stream GetManifestResourceStream(string filename)
        {
            var assembly = Assembly.GetCallingAssembly();
            var fullname = assembly.GetManifestResourceNames().SingleOrDefault(x => x.EndsWith(filename));
            if (!string.IsNullOrEmpty(fullname))
            {
                return assembly.GetManifestResourceStream(fullname);
            }

            return null;
        }

        private T LoadEmbeddedJsonFile<T>(string filename) where T : class
        {
            string jsonFileText = String.Empty;

            using (StreamReader reader = new StreamReader(LoadEmbeddedJsonStream(filename)))
            {
                jsonFileText = reader.ReadToEnd();
            }

            T result;

            try
            {
                var jsonParameters = new JSONParameters
                {
                    AutoConvertStringToNumbers = true,
                };
                result = string.IsNullOrEmpty(jsonFileText) ? null : JSON.ToObject<T>(jsonFileText, jsonParameters);
            }
            catch (Exception)
            {
                Logger.LogError($"Could not parse file '{filename}'! Errors in JSON!");
                throw;
            }

            return result;
        }

        private Stream LoadEmbeddedJsonStream(string filename)
        {
            return this.GetManifestResourceStream(filename);
        }
    }
}

