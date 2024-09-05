/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Facepunch;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("One Seat Vehicle", "VisEntities", "1.0.1")]
    [Description("Only one person per vehicle, no uninvited passengers.")]
    public class OneSeatVehicle : RustPlugin
    {
        #region Fields

        private static OneSeatVehicle _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Vehicles")]
            public List<VehicleConfig> Vehicles { get; set; }
        }

        private class VehicleConfig
        {
            [JsonProperty("Vehicle Short Prefab Name")]
            public string VehicleShortPrefabName { get; set; }

            [JsonProperty("Prevent Mounting If Driver Inside")]
            public bool PreventMountingIfDriverInside { get; set; }

            [JsonProperty("Prevent Mounting If Passenger Inside")]
            public bool PreventMountingIfPassengerInside { get; set; }

            [JsonProperty("Allow Teammates To Mount")]
            public bool AllowTeammatesToMount { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                Vehicles = new List<VehicleConfig>
                {
                    new VehicleConfig
                    {
                        VehicleShortPrefabName = "minicopter.entity",
                        PreventMountingIfDriverInside = true,
                        PreventMountingIfPassengerInside = true,
                        AllowTeammatesToMount = true,
                    },
                    new VehicleConfig
                    {
                        VehicleShortPrefabName = "attackhelicopter.entity",
                        PreventMountingIfDriverInside = true,
                        PreventMountingIfPassengerInside = true,
                        AllowTeammatesToMount = true,
                    },
                    new VehicleConfig
                    {
                        VehicleShortPrefabName = "rowboat",
                        PreventMountingIfDriverInside = true,
                        PreventMountingIfPassengerInside = true,
                        AllowTeammatesToMount = true,
                    },
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (player == null || mountable == null)
                return null;

            BaseVehicle vehicle = mountable.GetParentEntity() as BaseVehicle;
            if (vehicle == null)
                return null;

            if (PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                return null;

            VehicleConfig vehicleConfig = _config.Vehicles.Find(v => vehicle.ShortPrefabName.Contains(v.VehicleShortPrefabName));
            if (vehicleConfig == null)
                return null;

            if (vehicle.NumMounted() == 0)
                return null;

            if (vehicleConfig.AllowTeammatesToMount)
            {
                List<BasePlayer> mountedPlayers = Pool.Get<List<BasePlayer>>();
                vehicle.GetMountedPlayers(mountedPlayers);

                bool isTeammate = false;
                foreach (BasePlayer mountedPlayer in mountedPlayers)
                {
                    if (AreTeammates(mountedPlayer.userID, player.userID))
                    {
                        isTeammate = true;
                        break;
                    }
                }
                Pool.FreeUnmanaged(ref mountedPlayers);

                if (isTeammate)
                    return null;
            }

            bool hasDriver = vehicle.HasDriver();
            bool hasPassenger = vehicle.HasPassenger();

            if (hasDriver && vehicleConfig.PreventMountingIfDriverInside)
            {
                SendMessage(player, Lang.CannotMountVehicle);
                return false;
            }

            if (hasPassenger && vehicleConfig.PreventMountingIfPassengerInside)
            {
                SendMessage(player, Lang.CannotMountVehicle);
                return false;
            }

            return null;
        }

        #endregion Oxide Hooks

        #region Helper Functions

        public static bool AreTeammates(ulong firstPlayerId, ulong secondPlayerId)
        {
            RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindPlayersTeam(firstPlayerId);
            if (team != null && team.members.Contains(secondPlayerId))
                return true;

            return false;
        }

        #endregion Helper Functions

        #region Permissions

        private static class PermissionUtil
        {
            public const string IGNORE = "oneseatvehicle.ignore";
            private static readonly List<string> _permissions = new List<string>
            {
                IGNORE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permission

        #region Localization

        private class Lang
        {
            public const string CannotMountVehicle = "CannotMountVehicle";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.CannotMountVehicle] = "You cannot mount this vehicle as it already has an occupant.",
            }, this, "en");
        }

        private void SendMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = lang.GetMessage(messageKey, this, player.UserIDString);
            if (args.Length > 0)
                message = string.Format(message, args);

            SendReply(player, message);
        }

        #endregion Localization
    }
}