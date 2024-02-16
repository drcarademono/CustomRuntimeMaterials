using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Utility;
using System.IO;
using FullSerializer;

namespace CustomRuntimeMaterials
{
    [Serializable]
    public class MaterialDefinition
    {
        public int archive;
        public int record;
        public int frame;
    }

    [Serializable]
    public class ClimateMaterials
    {
        public MaterialDefinition[] defaultMaterials;
        public MaterialDefinition[] winterMaterials;
    }

    [Serializable]
    public class ClimateMaterialSettings
    {
        public ClimateMaterials ocean = new ClimateMaterials();
        public ClimateMaterials desert = new ClimateMaterials();
        public ClimateMaterials desert2 = new ClimateMaterials();
        public ClimateMaterials mountain = new ClimateMaterials();
        public ClimateMaterials rainforest = new ClimateMaterials();
        public ClimateMaterials swamp = new ClimateMaterials();
        public ClimateMaterials subtropical = new ClimateMaterials();
        public ClimateMaterials mountainWoods = new ClimateMaterials();
        public ClimateMaterials woodlands = new ClimateMaterials();
        public ClimateMaterials hauntedWoodlands = new ClimateMaterials();
    }

    [ImportedComponent]
    public class CustomRuntimeMaterialsMod : MonoBehaviour
    {
        private ClimateMaterialSettings climateMaterialSettings;
        private MeshRenderer meshRenderer;
        private static readonly fsSerializer _serializer = new fsSerializer();

        static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            Debug.Log("CustomRuntimeMaterialsMod: Init called.");
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<CustomRuntimeMaterialsMod>();        
        }

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            Debug.Log($"[CustomRuntimeMaterials] Awake called for {gameObject.name}");
            LoadClimateMaterialSettings();
        }

        private void Start()
        {
            Debug.Log("[CustomRuntimeMaterials] Start called");
            UpdateMaterialBasedOnClimateAndSeason();
        }

        private void LoadClimateMaterialSettings()
        {
            string cleanName = gameObject.name.Replace("(Clone)", "").Trim();
            string filePath = Path.Combine(Application.streamingAssetsPath, "CustomRuntimeMaterials", cleanName + ".json");
            Debug.Log($"[CustomRuntimeMaterials] Attempting to load JSON from {filePath}");

            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                Debug.Log($"[CustomRuntimeMaterials] JSON loaded successfully, contents: {json.Substring(0, Math.Min(json.Length, 500))}...");

                fsResult result = _serializer.TryDeserialize(fsJsonParser.Parse(json), ref climateMaterialSettings);
                if (!result.Succeeded)
                    Debug.LogError($"[CustomRuntimeMaterials] Deserialization failed: {result.FormattedMessages}");
                else
                    Debug.Log("[CustomRuntimeMaterials] Deserialization succeeded");
            }
            else
            {
                Debug.LogError("[CustomRuntimeMaterials] JSON file for material settings not found");
                climateMaterialSettings = new ClimateMaterialSettings(); // Fallback to default
            }
        }

        private Material[] LoadMaterialsFromDefinitions(MaterialDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
                return null;

            List<Material> materials = new List<Material>();
            foreach (var def in definitions)
            {
                Material loadedMaterial = null;
                Rect rectOut; // Required by GetMaterial but not necessarily used afterwards in this context

                // Get the MaterialReader instance from DaggerfallUnity
                MaterialReader materialReader = DaggerfallUnity.Instance.MaterialReader;

                // Attempt to use GetMaterial first
                loadedMaterial = materialReader.GetMaterial(def.archive, def.record, def.frame, 0, out rectOut, 0, false, false);

                if (loadedMaterial == null)
                {
                    // Fallback to TryImportMaterial if GetMaterial fails
                    if (TextureReplacement.TryImportMaterial(def.archive, def.record, def.frame, out loadedMaterial))
                    {
                        materials.Add(loadedMaterial);
                    }
                    else
                    {
                        Debug.LogWarning($"Could not load material for archive: {def.archive}, record: {def.record}, frame: {def.frame}");
                        materials.Add(null); // Handle missing materials as needed
                    }
                }
                else
                {
                    materials.Add(loadedMaterial); // Successfully loaded with GetMaterial
                }
            }
            return materials.ToArray();
        }

        private ClimateMaterials GetMaterialsForClimate(MapsFile.Climates climate, bool isWinter)
        {
            // Function to select materials based on climate and whether it's winter
            Func<ClimateMaterials, MaterialDefinition[]> selectMaterials = (materials) =>
                isWinter ? materials.winterMaterials : materials.defaultMaterials;

            // Initial selection based on the current climate
            ClimateMaterials selectedMaterials = climateMaterialSettings.woodlands; // Default fallback

            // Define the primary materials based on the current climate
            switch (climate)
            {
                case MapsFile.Climates.Ocean:
                    selectedMaterials = climateMaterialSettings.ocean;
                    break;
                case MapsFile.Climates.Desert:
                    selectedMaterials = climateMaterialSettings.desert;
                    break;
                case MapsFile.Climates.Desert2:
                    selectedMaterials = climateMaterialSettings.desert2;
                    break;
                case MapsFile.Climates.Mountain:
                    selectedMaterials = climateMaterialSettings.mountain;
                    break;
                case MapsFile.Climates.Rainforest:
                    selectedMaterials = climateMaterialSettings.rainforest;
                    break;
                case MapsFile.Climates.Swamp:
                    selectedMaterials = climateMaterialSettings.swamp;
                    break;
                case MapsFile.Climates.Subtropical:
                    selectedMaterials = climateMaterialSettings.subtropical;
                    break;
                case MapsFile.Climates.MountainWoods:
                    selectedMaterials = climateMaterialSettings.mountainWoods;
                    break;
                case MapsFile.Climates.HauntedWoodlands:
                    selectedMaterials = climateMaterialSettings.hauntedWoodlands;
                    break;
            }

            // Check if the selected materials are available; if not, use the fallback
            if (selectMaterials(selectedMaterials) == null || selectMaterials(selectedMaterials).Length == 0)
            {
                // Fallback logic
                switch (climate)
                {
                    case MapsFile.Climates.Ocean:
                    case MapsFile.Climates.Desert:
                    case MapsFile.Climates.Mountain:
                    case MapsFile.Climates.Rainforest:
                    case MapsFile.Climates.MountainWoods:
                    case MapsFile.Climates.HauntedWoodlands:
                        selectedMaterials = climateMaterialSettings.woodlands; // Fallback to Woodlands
                        break;
                    case MapsFile.Climates.Desert2:
                        selectedMaterials = climateMaterialSettings.desert; // Fallback to Desert
                        break;
                    case MapsFile.Climates.Swamp:
                        selectedMaterials = climateMaterialSettings.rainforest; // Fallback to Rainforest
                        break;
                    case MapsFile.Climates.Subtropical:
                        selectedMaterials = climateMaterialSettings.desert; // Fallback to Desert
                        break;
                }
            }

            return selectedMaterials;
        }

        private void UpdateMaterialBasedOnClimateAndSeason()
        {
            MapsFile.Climates currentClimate = GetCurrentClimate();
            bool isWinter = IsWinter(); // Determine if it's winter

            // Pass 'isWinter' to 'GetMaterialsForClimate'
            ClimateMaterials materialsForClimate = GetMaterialsForClimate(currentClimate, isWinter);

            // Determine which set of MaterialDefinition to use based on season
            MaterialDefinition[] definitions = isWinter ? materialsForClimate.winterMaterials : materialsForClimate.defaultMaterials;

            // Ensure definitions is not null or empty before proceeding
            if (definitions == null || definitions.Length == 0)
            {
                Debug.LogError("[CustomRuntimeMaterials] No definitions found for the current climate and season.");
                return;
            }

            // Load materials from the definitions
            Material[] selectedMaterials = LoadMaterialsFromDefinitions(definitions);

            // Apply the loaded materials to the meshRenderer
            if (selectedMaterials != null && selectedMaterials.Length > 0 && meshRenderer != null)
            {
                meshRenderer.materials = selectedMaterials;
            }
            else
            {
                Debug.LogError("[CustomRuntimeMaterials] No valid materials found for the current climate and season.");
            }
        }

        private MapsFile.Climates GetCurrentClimate()
        {
            return (MapsFile.Climates)GameManager.Instance.PlayerGPS.CurrentClimateIndex;
        }

        private bool IsWinter()
        {
            DaggerfallDateTime now = DaggerfallUnity.Instance.WorldTime.Now;
            return now.SeasonValue == DaggerfallDateTime.Seasons.Winter;
        }
    }
}
