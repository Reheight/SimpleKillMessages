using Newtonsoft.Json;
using Rust;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SimpleKillMessages", "Reheight", "1.1.1")]
    [Description("Displayed death/kill information in chat upon death, with some extra features!")]
    class SimpleKillMessages : CovalencePlugin
    {
        [PluginReference]
        private Plugin Economics;

        PluginConfig _config;

        private void Init()
        {
            _config = Config.ReadObject<PluginConfig>();
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }

                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning($"PluginConfig file {Name}.json updated.");

                    SaveConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();

                PrintError("Config file contains an error and has been replaced with the default file.");
            }

        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Prefix", Order = 0)]
            public string Prefix { get; set; }

            [JsonProperty(PropertyName = "Chat Icon", Order = 1)]
            public int ChatIcon { get; set; }

            [JsonProperty(PropertyName = "Prevent NPC", Order = 2)]
            public bool PreventNPC { get; set; }

            [JsonProperty(PropertyName = "Keep Held Item In Hotbar On Death", Order = 3)]
            public bool PreventDropOnDeath { get; set; }

            [JsonProperty(PropertyName = "Reward Kill (Economics)")]
            public bool EconomicsRewardsEnabled { get; set; }

            [JsonProperty(PropertyName = "Points Per Kill")]
            public double EconomicsPointsReward { get; set; }

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                Prefix = "<color=#42f566>SERVER:</color> ",
                ChatIcon = 0,
                PreventNPC = true,
                PreventDropOnDeath = true,
                EconomicsRewardsEnabled = false,
                EconomicsPointsReward = 2
            };
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DeathByWoundsVictim"] = "<size=12><color=#b6bab7>You bled out after being attacked by <color=#61ff7e>{0}</color>!</color></size>",
                ["DeathByWoundsKiller"] = "<size=12><color=#b6bab7>You killed <color=#61ff7e>{0}</color> as they bled out!</color></size>",
                ["DeathBySuicide"] = "<size=12><color=#b6bab7>You died by suicide!</color></size>",
                ["DeathByBurningVictim"] = "<size=12><color=#b6bab7>You were burned to death by <color=#61ff7e>{0}</color>!</color></size>",
                ["DeathByBurningKiller"] = "<size=12><color=#b6bab7>You burned <color=#61ff7e>{0}</color> to death!</color></size>",
                ["DeathByMeleeVictim"] = "<size=12><color=#b6bab7>You were killed by <color=#61ff7e>{0}</color> with a <color=#61ff7e>{1}</color>!</color></size>",
                ["DeathByMeleeKiller"] = "<size=12><color=#b6bab7>You killed <color=#61ff7e>{0}</color> with a <color=#61ff7e>{1}</color>!</color></size>",
                ["DeathByExplosionVictim"] = "<size=12><color=#b6bab7>You were exploded by <color=#61ff7e>{0}</color> with a <color=#61ff7e>{1}</color>!</color></size>",
                ["DeathByExplosionKiller"] = "<size=12><color=#b6bab7>You exploded <color=#61ff7e>{0}</color> with a <color=#61ff7e>{1}</color>!</color></size>",
                ["DeathByProjectileVictim"] = "<size=12><color=#b6bab7>You were killed by <color=#61ff7e>{0}</color> with a <color=#61ff7e>{1}</color> from <color=#61ff7e>{2} meters</color> with a shot to your <color=#61ff7e>{3}</color>!</color></size>",
                ["DeathByProjectileKiller"] = "<size=12><color=#b6bab7>You killed <color=#61ff7e>{0}</color> with a <color=#61ff7e>{1}</color> from <color=#61ff7e>{2}</color> meters with a shot to their <color=#61ff7e>{3}</color>!</color></size>",
                ["RewardedForKill"] = "<size=12><color=#b6bab7>You have received a total of <color=#61ff7e>{0} RP</color> for killing <color=#61ff7e>{1}</color>!</color></size>"
            }, this);
        }

        private string Lang(string key, params object[] args) => _config.Prefix + String.Format(lang.GetMessage(key, this, _config.ChatIcon.ToString()), args);

        object CanDropActiveItem(BasePlayer player)
        {
            if (player == null || !_config.PreventDropOnDeath)
                return null;
            return false;
        }

        private static bool IsExplosion(HitInfo hit) => (hit.WeaponPrefab != null && (hit.WeaponPrefab.ShortPrefabName.Contains("grenade") || hit.WeaponPrefab.ShortPrefabName.Contains("explosive")))
                                                        || hit.damageTypes.GetMajorityDamageType() == DamageType.Explosion || (!hit.damageTypes.IsBleedCausing() && hit.damageTypes.Has(DamageType.Explosion));

        private void OnPlayerDeath(BasePlayer entity, HitInfo info)
        {
            if (entity == null) return;

            if (_config.PreventNPC && entity.IsNpc ||
                _config.PreventNPC && !entity.userID.IsSteamId() ||
                _config.PreventNPC && entity.UserIDString.Length != 17) return;

            if (info == null)
            {
                if (!entity.lastAttacker) return;

                BasePlayer wAttacker = entity.lastAttacker.ToPlayer();

                if (wAttacker == null ||
                _config.PreventNPC && wAttacker.IsNpc ||
                _config.PreventNPC && !wAttacker.userID.IsSteamId() ||
                _config.PreventNPC && wAttacker.UserIDString.Length != 17) return;

                if (wAttacker != null && entity.IsWounded())
                {
                    DeathFromWounds(entity, wAttacker);
                }
                return;
            }

            BasePlayer attacker = info.InitiatorPlayer;

            if (attacker == null) return;

            if (_config.PreventNPC && attacker.IsNpc ||
                _config.PreventNPC && !attacker.userID.IsSteamId() ||
                _config.PreventNPC && attacker.UserIDString.Length != 17) return;

            if (attacker == entity)
            {
                DeathFromSuicide(entity);
                return;
            }

            if (IsExplosion(info))
            {
                DeathFromExplosion(entity, attacker, info);
                return;
            }

            if (info.damageTypes.GetMajorityDamageType() == DamageType.Heat || (!info.damageTypes.IsBleedCausing() && info.damageTypes.Has(DamageType.Heat)))
            {
                DeathFromBurning(entity, attacker);
                return;
            }

            if (!info.IsProjectile())
            {
                DeathFromMelee(entity, attacker, info);
                return;
            }

            string distance = GetDistance(entity, info);
            if (distance == null) return;

            DeathFromProjectile(entity, attacker, info, distance);

            if (_config.EconomicsRewardsEnabled && attacker != entity)
            {
                if (!Economics)
                {
                    LogError("You are attempting to reward players with Economics points but you do not seem to have economics installed.");
                    return;
                }

                Economics.Call("Deposit", attacker.userID, _config.EconomicsPointsReward);
                attacker.ChatMessage(Lang("RewardedForKill", _config.EconomicsPointsReward, entity.displayName));
            }
        }

        string GetDistance(BaseCombatEntity entity, HitInfo info)
        {
            float distance = 0.0f;

            if (entity != null && info.Initiator != null)
            {
                distance = Vector3.Distance(info.Initiator.transform.position, entity.transform.position);
            }
            return distance.ToString("0.0").Equals("0.0") ? "" : distance.ToString("0.0") + "m";
        }

        private void DeathFromWounds(BasePlayer victim, BasePlayer attacker)
        {
            victim.ChatMessage(Lang("DeathByWoundsVictim", attacker.displayName));
            attacker.ChatMessage(Lang("DeathByWoundsKiller", victim.displayName));
        }

        private void DeathFromExplosion(BasePlayer victim, BasePlayer attacker, HitInfo info)
        {
            victim.ChatMessage(Lang("DeathByExplosionVictim", attacker.displayName, info.WeaponPrefab.name));
            attacker.ChatMessage(Lang("DeathByExplosionKiller", victim.displayName, info.WeaponPrefab.name));
        }

        private void DeathFromSuicide(BasePlayer victim)
        {
            victim.ChatMessage(Lang("DeathBySuicide"));
        }

        private void DeathFromBurning(BasePlayer victim, BasePlayer attacker)
        {
            victim.ChatMessage(Lang("DeathByBurningVictim", attacker.displayName));
            attacker.ChatMessage(Lang("DeathByBurningKiller", victim.displayName));
        }

        private void DeathFromMelee(BasePlayer victim, BasePlayer attacker, HitInfo info)
        {
            AttackEntity wpn = info.Weapon;

            if (wpn == null ||
                wpn.GetItem() == null ||
                wpn.GetItem().info == null ||
                wpn.GetItem().info.displayName == null ||
                wpn.GetItem().info.displayName.english == null) return;

            victim.ChatMessage(Lang("DeathByMeleeVictim", attacker.displayName, wpn.GetItem().info.displayName.english));
            attacker.ChatMessage(Lang("DeathByMeleeKiller", victim.displayName, wpn.GetItem().info.displayName.english));
        }

        private void DeathFromProjectile(BasePlayer victim, BasePlayer attacker, HitInfo info, string dist)
        {
            AttackEntity wpn = info.Weapon;
            string lastShotLoc = info.boneName;


            if (wpn == null ||
                wpn.GetItem() == null ||
                wpn.GetItem().info == null ||
                wpn.GetItem().info.displayName == null ||
                wpn.GetItem().info.displayName.english == null) return;

            victim.ChatMessage(Lang("DeathByProjectileVictim", attacker.displayName, wpn.GetItem().info.displayName.english, dist, lastShotLoc));
            attacker.ChatMessage(Lang("DeathByProjectileKiller", victim.displayName, wpn.GetItem().info.displayName.english, dist, lastShotLoc));
        }
    }
}

