using System;
using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    [RequireComponent(typeof(Camera))]
    public class FogVisibilityManager : MonoBehaviour
    {
        private Camera fogOfWarCamera;

        private Texture2D visionTexture;
        private Rect textureRect;

        private HashSet<IHideable> hideables = new(1000);

        private void Awake()
        {
            fogOfWarCamera = GetComponent<Camera>();
            visionTexture = new Texture2D(fogOfWarCamera.targetTexture.width, fogOfWarCamera.targetTexture.height);
            textureRect = new Rect(0, 0, visionTexture.width, visionTexture.height);

            Bus<UnitSpawnEvent>.RegisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);

            Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);

            Bus<PlaceholderSpawnEvent>.RegisterForAll(HandlePlaceholderSpawn);
            Bus<PlaceholderDestroyEvent>.RegisterForAll(HandlePlaceholderDestroy);

            Bus<SupplySpawnEvent>.OnEvent[Owner.Unowned] += HandleSupplySpawn;
            Bus<SupplyDepletedEvent>.OnEvent[Owner.Unowned] += HandleSupplyDepleted;
        }

        private void OnDestroy()
        {
            Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);

            Bus<BuildingSpawnEvent>.UnregisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeath);

            Bus<PlaceholderSpawnEvent>.UnregisterForAll(HandlePlaceholderSpawn);
            Bus<PlaceholderDestroyEvent>.UnregisterForAll(HandlePlaceholderDestroy);

            Bus<SupplySpawnEvent>.OnEvent[Owner.Unowned] -= HandleSupplySpawn;
            Bus<SupplyDepletedEvent>.OnEvent[Owner.Unowned] -= HandleSupplyDepleted;
        }

        private void LateUpdate()
        {
            ReadPixelsToVisionTexture();

            foreach(IHideable hideable in hideables)
            {
                SetUnitVisibilityStatus(hideable);
            }
        }

        private void ReadPixelsToVisionTexture()
        {
            RenderTexture previousRenderTexture = RenderTexture.active;

            RenderTexture.active = fogOfWarCamera.targetTexture;
            visionTexture.ReadPixels(textureRect, 0, 0);
            RenderTexture.active = previousRenderTexture;
        }

        private void SetUnitVisibilityStatus(IHideable hideable)
        {
            Vector3 screenPoint = fogOfWarCamera.WorldToScreenPoint(hideable.Transform.position);
            Color visibilityColor = visionTexture.GetPixel((int)screenPoint.x, (int)screenPoint.y);
            hideable.SetVisible(visibilityColor.r > 0.9f);
        }

        private void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit.Owner != Owner.Player1)
            {
                hideables.Add(evt.Unit);
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            hideables.Remove(evt.Unit);
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building.Owner != Owner.Player1)
            {
                hideables.Add(evt.Building);
            }
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            hideables.Remove(evt.Building);
        }

        private void HandleSupplySpawn(SupplySpawnEvent evt)
        {
            hideables.Add(evt.Supply);
        }

        private void HandleSupplyDepleted(SupplyDepletedEvent evt)
        {
            hideables.Remove(evt.Supply);
        }

        private void HandlePlaceholderDestroy(PlaceholderDestroyEvent evt)
        {
            hideables.Remove(evt.Placeholder);
        }

        private void HandlePlaceholderSpawn(PlaceholderSpawnEvent evt)
        {
            hideables.Add(evt.Placeholder);
        }
    }
}