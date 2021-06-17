using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Personal Slot Machine", "bmgjet", "1.0.2")]
    [Description("Slot machine for personal use.")]
    public class PersonalSlotMachine : RustPlugin
    {
        #region Vars
        private const ulong skinID = 2514706320;
        private const string prefab = "assets/prefabs/misc/casino/slotmachine/slotmachine.prefab";
        private const string permUse = "PersonalSlotMachine.use";
        static List<string> effects = new List<string>
        {
        "assets/bundled/prefabs/fx/item_break.prefab",
        "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
        };
        private static PersonalSlotMachine plugin;
        #endregion

        #region Language
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
            {"Name", "Slot Machine"},
            {"Pickup", "You picked up slot machine!"},
            {"Receive", "You received slot machine!"},
            {"Permission", "You need permission to do that!"}
            }, this);
        }

        private void message(BasePlayer player, string key, params object[] args)
        {
            if (player == null) { return; }
            var message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
            player.ChatMessage(message);
        }
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            plugin = this;
            CheckSlotMachines();
        }

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        private void Unload()
        {
            effects = null;
            plugin = null;
        }

        private void OnEntityBuilt(Planner plan, GameObject go) { CheckDeploy(go.ToBaseEntity()); }

        private void OnHammerHit(BasePlayer player, HitInfo info) { CheckHit(player, info?.HitEntity); }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null)
                return null;

            if (entity.name.Contains("slotmachine.prefab") && entity.OwnerID != 0)
            {
                SlotMachineAddon CurrentSlotMachine = entity.GetComponent<SlotMachineAddon>();
                if (CurrentSlotMachine != null)
                {
                    int GiveDamage = 100;
                    try
                    {
                                string Damage = info.damageProperties.name.ToString();
                                if (Damage.Contains("Melee")) { GiveDamage = 5; }
                                else if (Damage.Contains("Buckshot")) { GiveDamage = 9; }
                                else if (Damage.Contains("Arrow")) { GiveDamage = 15; }
                                else if (Damage.Contains("Pistol")) { GiveDamage = 20; }
                                else if (Damage.Contains("Rifle")) { GiveDamage = 25; }
                     }
                    catch { }
                        var CurrentHealth = CurrentSlotMachine.SlotProtection.amounts.GetValue(0);
                        int ChangeHealth = int.Parse(CurrentHealth.ToString()) - GiveDamage;
                        CurrentSlotMachine.SlotProtection.amounts.SetValue((object)ChangeHealth, 0);
                        if (ChangeHealth <= 0)
                        {
                            foreach (var effect in effects) { Effect.server.Run(effect, entity.transform.position); }
                            entity.Kill();
                        }
                }
            }
            return null;
        }
        #endregion

        #region Core
        private void SpawnSlotMachine(Vector3 position, Quaternion rotation = default(Quaternion), ulong ownerID = 0)
        {
            var slotmachine = GameManager.server.CreateEntity(prefab, position, rotation);
            if (slotmachine == null) { return; }
            slotmachine.skinID = skinID;
            slotmachine.OwnerID = ownerID;
            slotmachine.gameObject.AddComponent<SlotMachineAddon>();
            slotmachine.Spawn();
        }

        private void CheckSlotMachines()
        {
            foreach (var slotmachines in GameObject.FindObjectsOfType<SlotMachine>())
            {
                var x = slotmachines;
                if (x is SlotMachine && x.OwnerID != 0 && x.GetComponent<SlotMachineAddon>() == null)
                {
                    Puts("Found Personal Slot Machine " + slotmachines.ToString() + " " + slotmachines.OwnerID.ToString() + " Adding Component");
                    slotmachines.gameObject.AddComponent<SlotMachineAddon>();
                }
            }
        }

        private void GiveSlotMachine(BasePlayer player, bool pickup = false)
        {
            var item = CreateItem();
            if (item != null && player != null)
            {
                player.GiveItem(item);
                message(player, pickup ? "Pickup" : "Receive");
            }
        }

        private Item CreateItem()
        {
            var item = ItemManager.CreateByName("fridge", 1, skinID);
            if (item != null)
            {
                item.text = "Slot Machine";
                item.name = item.text;
            }
            return item;
        }

        private void CheckDeploy(BaseEntity entity)
        {
            if (entity == null) { return; }
            if (!IsSlotMachine(entity.skinID)) { return; }
            SpawnSlotMachine(entity.transform.position, entity.transform.rotation, entity.OwnerID);
            NextTick(() => { entity?.Kill(); });
        }

        private void CheckHit(BasePlayer player, BaseEntity entity)
        {
            if (entity == null) { return; }
            if (!IsSlotMachine(entity.skinID)) { return; }
            entity.GetComponent<SlotMachineAddon>()?.TryPickup(player);
        }

        [ChatCommand("slotmachine.craft")]
        private void Craft(BasePlayer player)
        {
            if (CanCraft(player)) { GiveSlotMachine(player); }
        }

        private bool CanCraft(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                message(player, "Permission");
                return false;
            }
            return true;
        }
        #endregion

        #region Helpers
        private bool IsSlotMachine(ulong skin) { return skin != 0 && skin == skinID; }
        #endregion

        #region Command
        [ConsoleCommand("slotmachine.give")]
        private void Cmd(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin && arg.Args?.Length > 0)
            {
                var player = BasePlayer.Find(arg.Args[0]) ?? BasePlayer.FindSleeping(arg.Args[0]);
                if (player == null)
                {
                    PrintWarning($"Can't find player with that name/ID! {arg.Args[0]}");
                    return;
                }
                GiveSlotMachine(player);
            }
        }
        #endregion

        #region Scripts
        private class SlotMachineAddon : MonoBehaviour
        {
            private SlotMachine slotmachine;
            public ulong OwnerId;
            public ProtectionProperties SlotProtection = ScriptableObject.CreateInstance<ProtectionProperties>();

            private void Awake()
            {
                slotmachine = GetComponent<SlotMachine>();
                SlotProtection.Add(100f);
                InvokeRepeating("CheckGround", 5f, 5f);
            }

            private void CheckGround()
            {
                RaycastHit rhit;
                var cast = Physics.Raycast(slotmachine.transform.position + new Vector3(0, 0.1f, 0), Vector3.down,
                    out rhit, 4f, LayerMask.GetMask("Terrain", "Construction"));
                var distance = cast ? rhit.distance : 3f;
                if (distance > 0.2f) { GroundMissing(); }
            }

            private void GroundMissing()
            {
                foreach (var effect in effects) { Effect.server.Run(effect, slotmachine.transform.position); }
                this.DoDestroy();
            }

            public void TryPickup(BasePlayer player)
            {
                this.DoDestroy();
                plugin.GiveSlotMachine(player, true);
            }

            public void DoDestroy()
            {
                var entity = slotmachine;
                try { entity.Kill(); } catch { }
            }
        }
        #endregion
    }
}