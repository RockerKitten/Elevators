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

namespace BuildTemplate
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class BuildTemplate : BaseUnityPlugin
    {
        public const string PluginGUID = "com.RockerKitten.BuildTemplate";
        public const string PluginName = "BuildTemplate";
        public const string PluginVersion = "1.0.0";

        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private AssetBundle BuildAssetBundle { get; set; }
        //private AudioSource fireAudioSource;

        private Dictionary<BuildMaterial, BuildEffectLists> effects;

        private void Awake()
        {
            LoadEmbeddedAssembly("fastJSON.dll");
            this.BuildAssetBundle = AssetUtils.LoadAssetBundleFromResources("BuildTemplate", Assembly.GetExecutingAssembly());

            PrefabManager.OnVanillaPrefabsAvailable += SetupAssets;
            Jotunn.Logger.LogInfo("BuildTemplate has landed");
        }

        private void SetupAssets()
        {
            this.effects = InitializeEffects();
            InitializeBuildConstructionTools();
            InitializeBuildAssets();
            PrefabManager.OnVanillaPrefabsAvailable -= SetupAssets;
        }

        private void InitializeBuildConstructionTools()
        {
            if (Chainloader.PluginInfos.ContainsKey("com.RockerKitten.CastleScepter"))
            {
                TableName = "_RKC_CustomTable";
            }
            else
            {
                TableName = "_HammerPieceTable";
            }
        }

        private void InitializeBuildAssets()
        {
            var BuildAssets = LoadEmbeddedJsonFile<BuildAssets>("Buildassets.json");

            foreach (var BuildPieceTable in BuildAssets.PieceTables)
            {
                foreach (var BuildPieceCategory in BuildPieceTable.Categories)
                {
                    foreach (var BuildPiece in BuildPieceCategory.Pieces)
                    {
                        var customPiece = this.BuildCustomPiece(BuildPieceTable, BuildPieceCategory, BuildPiece);

                        // load supplemental assets (sfx and vfx)
                        this.AttachEffects(customPiece.PiecePrefab, BuildPiece);

                        PieceManager.Instance.AddPiece(customPiece);
                    }
                }
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
                        Hit   = createfxlist("vfx_SawDust"),
                        Open  = createfxlist("sfx_door_open"),
                        Close = createfxlist("sfx_door_close"),
                        Fuel  = createfxlist("vfx_HearthAddFuel"),
                    }
                },
                {
                    BuildMaterial.Stone,
                    new BuildEffectLists
                    {
                        Place = createfxlist("sfx_build_hammer_stone", "vfx_Place_stone_wall_2x1"),
                        Break = createfxlist("sfx_rock_destroyed", "vfx_Place_stone_wall_2x1"),
                        Hit   = createfxlist("sfx_Rock_Hit"),
                        Open  = createfxlist("sfx_door_open"),
                        Close = createfxlist("sfx_door_close"),
                        Fuel  = createfxlist("vfx_HearthAddFuel"),
                    }
                },
                {
                    BuildMaterial.Metal,
                    new BuildEffectLists
                    {
                        Place = createfxlist("sfx_build_hammer_metal", "vfx_Place_stone_wall_2x1"),
                        Break = createfxlist("sfx_rock_destroyed", "vfx_HitSparks"),
                        Hit   = createfxlist("vfx_HitSparks"),
                        Open  = createfxlist("sfx_door_open"),
                        Close = createfxlist("sfx_door_close"),
                        Fuel  = createfxlist("vfx_HearthAddFuel"),
                    }
                }
            };

            return effects;
        }

        //private void AddLocalizations()
        //{
        //    CustomLocalization customLocalization = new CustomLocalization();
        //    customLocalization.AddTranslation("English", new Dictionary<String, String>
        //    {
        //        { "piece_wallrkc", "Wall" }
        //    });
        //}

        private CustomPiece BuildCustomPiece(BuildPieceTable BuildPieceTable, BuildPieceCategories BuildPieceCategory, BuildPiece BuildPiece)
        {
            var BuildPiecePrefab = this.BuildAssetBundle.LoadAsset<GameObject>(BuildPiece.PrefabName);

            var pieceConfig = new PieceConfig();
            // TODO: verify token string
            pieceConfig.Name = BuildPiece.DisplayNameToken;
            pieceConfig.Description = BuildPiece.PrefabDescription;
            // NOTE: could move override to json config if needed.
            pieceConfig.AllowedInDungeons = false;
            pieceConfig.PieceTable = BuildPieceTable.TableName;
            pieceConfig.Category = BuildPieceCategory.CategoryTabName;
            pieceConfig.Enabled = BuildPiece.Enabled;
            if (!string.IsNullOrWhiteSpace(BuildPiece.RequiredStation))
            {
                pieceConfig.CraftingStation = BuildPiece.RequiredStation;
            }

            var requirements = BuildPiece.Requirements
                .Select(r => new RequirementConfig(r.Item, r.Amount, recover: r.Recover));

            pieceConfig.Requirements = requirements.ToArray();
            var customPiece = new CustomPiece(BuildPiecePrefab, fixReference: false, pieceConfig);
            var material = BuildPiecePrefab.GetComponentsInChildren<Material>();
            foreach (Material mat in material)
            {
                if (mat.name == "replace")
                {
                    mat.shader = Shader.Find("Custom/Piece");
                }
            }
            Jotunn.Logger.LogInfo(BuildPiecePrefab.name);
            return customPiece;
        }

        private void AttachEffects(GameObject piecePrefab, BuildPiece BuildPiece)
        {
            var pieceComponent = piecePrefab.GetComponent<Piece>();
            pieceComponent.m_placeEffect = this.effects[BuildPiece.Material].Place;

            var wearComponent = piecePrefab.GetComponent<WearNTear>();
            wearComponent.m_destroyedEffect = this.effects[BuildPiece.Material].Break;
            wearComponent.m_hitEffect = this.effects[BuildPiece.Material].Hit;

            if (piecePrefab.TryGetComponent<Door>(out Door doorComponent))
            {
                doorComponent.m_openEffects = this.effects[BuildPiece.Material].Open;
                doorComponent.m_closeEffects = this.effects[BuildPiece.Material].Close;
            }

            if (piecePrefab.TryGetComponent<Fireplace>(out Fireplace fireplaceComponent))
            {
                fireplaceComponent.m_fuelAddedEffects = this.effects[BuildPiece.Material].Fuel;
                //fireplaceComponent.m_fuelItem = this.[BuildPiece.FuelItem];
                // how to add fuel type?
                //fireAudioSource = piecePrefab.GetComponentInChildren<AudioSource>();
                //fireAudioSource.outputAudioMixerGroup = AudioMan.instance.m_ambientMixer;
            }

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

