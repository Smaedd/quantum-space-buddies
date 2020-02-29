﻿using OWML.ModHelper.Events;
using System.Linq;
using UnityEngine;

namespace QSB
{
    class RespawnOnDeath : MonoBehaviour
    {
        static RespawnOnDeath Instance;

        SpawnPoint _shipSpawnPoint;
        SpawnPoint _playerSpawnPoint;
        OWRigidbody _shipBody;
        PlayerSpawner _playerSpawner;
        FluidDetector _fluidDetector;
        PlayerResources _playerResources;
        ShipComponent[] _shipComponents;
        HatchController _hatchController;
        ShipCockpitController _cockpitController;
        PlayerSpacesuit _spaceSuit;

        void Awake()
        {
            GlobalMessenger.AddListener("WakeUp", PlayerWokeUp);

            Instance = this;
            QSB.Helper.HarmonyHelper.AddPrefix<DeathManager>("KillPlayer", typeof(Patches), nameof(Patches.PreFinishDeathSequence));
        }

        void PlayerWokeUp()
        {
            var playerTransform = Locator.GetPlayerTransform();
            _playerResources = Locator.GetPlayerTransform().GetComponent<PlayerResources>();
            _spaceSuit = Locator.GetPlayerTransform().GetComponentInChildren<PlayerSpacesuit>(true);
            _playerSpawner = FindObjectOfType<PlayerSpawner>();
            _fluidDetector = Locator.GetPlayerCamera().GetComponentInChildren<FluidDetector>();

            var shipTransform = Locator.GetShipTransform();
            if (shipTransform)
            {
                _shipComponents = shipTransform.GetComponentsInChildren<ShipComponent>();
                _hatchController = shipTransform.GetComponentInChildren<HatchController>();
                _cockpitController = shipTransform.GetComponentInChildren<ShipCockpitController>();
                _shipBody = Locator.GetShipBody();
                _shipSpawnPoint = GetSpawnPoint(true);

                // Move debug spawn point to initial ship position.
                _playerSpawnPoint = GetSpawnPoint();
                _shipSpawnPoint.transform.position = shipTransform.position;
                _shipSpawnPoint.transform.rotation = shipTransform.rotation;
            }

        }

        public void ResetShip()
        {
            if (!_shipBody)
            {
                return;
            }

            // Reset ship position.
            _shipBody.SetVelocity(_shipSpawnPoint.GetPointVelocity());
            _shipBody.WarpToPositionRotation(_shipSpawnPoint.transform.position, _shipSpawnPoint.transform.rotation);

            // Reset ship damage.
            if (Locator.GetShipTransform())
            {
                for (int i = 0; i < _shipComponents.Length; i++)
                {
                    _shipComponents[i].SetDamaged(false);
                }
            }

            Invoke(nameof(ExitShip), 0.01f);
        }

        void ExitShip()
        {
            _cockpitController.Invoke("ExitFlightConsole");
            _cockpitController.Invoke("CompleteExitFlightConsole");
            _hatchController.SetValue("_isPlayerInShip", false);
            _hatchController.Invoke("OpenHatch");
            GlobalMessenger.FireEvent("ExitShip");
        }

        public void ResetPlayer()
        {
            // Reset player position.
            OWRigidbody playerBody = Locator.GetPlayerBody();
            playerBody.WarpToPositionRotation(_playerSpawnPoint.transform.position, _playerSpawnPoint.transform.rotation);
            playerBody.SetVelocity(_playerSpawnPoint.GetPointVelocity());
            _playerSpawnPoint.AddObjectToTriggerVolumes(Locator.GetPlayerDetector().gameObject);
            _playerSpawnPoint.AddObjectToTriggerVolumes(_fluidDetector.gameObject);
            _playerSpawnPoint.OnSpawnPlayer();

            // Stop suffocation sound effect.
            _playerResources.SetValue("_isSuffocating", false);

            // Reset player health and resources.
            _playerResources.DebugRefillResources();

            // Remove space suit.
            _spaceSuit.RemoveSuit(true);
        }

        SpawnPoint GetSpawnPoint(bool isShip = false)
        {
            return _playerSpawner
                .GetValue<SpawnPoint[]>("_spawnList")
                .FirstOrDefault(spawnPoint =>
                    spawnPoint.GetSpawnLocation() == SpawnLocation.TimberHearth && spawnPoint.IsShipSpawn() == isShip
                );
        }

        internal static class Patches
        {
            public static bool PreFinishDeathSequence()
            {
                Instance.ResetShip();
                Instance.ResetPlayer();

                // Prevent original death method from running.
                return false;
            }
        }
    }
}