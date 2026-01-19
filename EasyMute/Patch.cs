using System;
using System.Reflection;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using ResoniteModLoader;

namespace EasyMute;

public class Patch : ResoniteMod
{
    public override string Name => "Easy Mute";
    public override string Author => "LeCloutPanda";
    public override string Version => "1.0.0-a";

    [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> ENABLED = new ModConfigurationKey<bool>("Enabled", "Global toggle for the mod.", () => true);
    private static ModConfiguration config;

    public override void OnEngineInit()
    {
        config = GetConfiguration();
        Harmony harmony = new Harmony("dev.lecloutpanda.easymute");
        harmony.PatchAll();
    }

    [HarmonyPatch]
    class ContextMenuPatch
    {   
        [HarmonyPostfix] 
        [HarmonyPatch(typeof(ContextMenu), "OnStart")]
        private static void OnAttachPostfix(ContextMenu __instance, SyncRef<Button> ____innerCircleButton, SyncRef<Image> ____iconImage) 
        {
            try {
                if (!config.GetValue(ENABLED)) return;
                if (__instance.World.IsUserspace()) return;
                if (__instance.Slot.ActiveUser != __instance.World.LocalUser) return;
                __instance.LocalUserRoot.RunInUpdates(3, () =>
                {
                    ____innerCircleButton.Target.Pressed.Clear();
                    ____innerCircleButton.Target.Released.Clear();
                    
                    ____innerCircleButton.Target.LocalPressed += (IButton button, ButtonEventData data) => {
                        __instance.AudioSystem.IsMuted = !__instance.AudioSystem.IsMuted;
                        __instance.RunInUpdates(3, () => { UpdateCenterIcon(__instance, ____innerCircleButton, ____iconImage); });
                    };   
                });
            }
            catch(Exception ex) 
            {
                UniLog.Error(ex.ToString());
            }
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ContextMenu), "OpenMenu")]
        private static void OpenMenuPostfix(ContextMenu __instance, SyncRef<Button> ____innerCircleButton, SyncRef<Image> ____iconImage) 
        {
            if (!config.GetValue(ENABLED)) return;
            if (__instance.World.IsUserspace()) return;
            if (__instance.Slot.ActiveUser != __instance.World.LocalUser) return;
            UpdateCenterIcon(__instance, ____innerCircleButton, ____iconImage);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ContextMenu), "ItemDeselected")]
        private static bool Change(ContextMenu __instance, ContextMenuItem item, SyncRef<Button> ____innerCircleButton, SyncRef<ContextMenuItem> ____selectedItem, SyncRef<Image> ____iconImage) 
        {
            try
            {
                if (!config.GetValue(ENABLED)) return true;
                if (__instance.World.IsUserspace()) return true;
                if (__instance.Slot.ActiveUser != __instance.World.LocalUser) return true;
                
                if (____selectedItem.Target == item)
                {
                    ____selectedItem.Target = null;
                    UpdateCenterIcon(__instance, ____innerCircleButton, ____iconImage);
                }
                lockVoiceModeChanges = false;
                return false;
            }
            catch (Exception ex)
            {
                UniLog.Error(ex.ToString());
                lockVoiceModeChanges = false;
                return true;
            }

        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ContextMenu), "ItemSelected")]
        private static void ItemSelectPatch(ContextMenu __instance) 
        {
            lockVoiceModeChanges = true;
        }

        private static bool lockVoiceModeChanges = false;
        private static VoiceMode lastVoiceMode = VoiceMode.Mute;
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(User), nameof(User.VoiceMode), MethodType.Setter)]
        private static void VoiceModePatch(User __instance)
        {
            try {
                if (!config.GetValue(ENABLED)) return;
                if (__instance.World.IsUserspace()) return;
                if (__instance != __instance.World.LocalUser) return;

                if (lockVoiceModeChanges == true) return;
                if (__instance.ActiveVoiceMode == lastVoiceMode) return;
                lastVoiceMode = __instance.ActiveVoiceMode;

                ContextMenu contextMenu = __instance.LocalUserRoot.LocalUserRoot.Slot.GetComponentInChildren<ContextMenu>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;

                var innerButtonField = contextMenu.GetType().GetField("_innerCircleButton", flags);
                if (innerButtonField == null)
                    throw new Exception("Field _innerCircleButton not found");
                var innerCircleButton = (SyncRef<Button>)innerButtonField.GetValue(contextMenu);

                var iconImageField = contextMenu.GetType().GetField("_iconImage", flags);
                if (iconImageField == null)
                    throw new Exception("Field _iconImage not found");
                var iconImage = (SyncRef<Image>)iconImageField.GetValue(contextMenu);
                    
                UpdateCenterIcon(contextMenu, innerCircleButton, iconImage);
            } 
            catch(Exception ex) 
            {
                UniLog.Error(ex.ToString());
            }
        }
        
        private static void UpdateCenterIcon(ContextMenu __instance, SyncRef<Button> ____innerCircleButton, SyncRef<Image> ____iconImage)
        {
            SpriteProvider sprite = ____iconImage.Target.Slot.GetComponentOrAttach<SpriteProvider>();
            StaticTexture2D staticTexture2D = ____iconImage.Target.Slot.GetComponentOrAttach<StaticTexture2D>();
            sprite.Persistent = false;
            staticTexture2D.Persistent = false;
            colorX voiceColor = colorX.Gray;
            Uri iconUri = null;
            switch (__instance.LocalUser.ActiveVoiceMode)
            {
                case VoiceMode.Mute:
                    voiceColor = RadiantUI_Constants.Hero.RED;
                    iconUri = OfficialAssets.Graphics.Icons.Voice.Mute;
                    break;
                case VoiceMode.Whisper:
                    voiceColor = RadiantUI_Constants.Hero.PURPLE;
                    iconUri = OfficialAssets.Graphics.Icons.Voice.Whisper;
                    break;
                case VoiceMode.Normal:
                    voiceColor = RadiantUI_Constants.Hero.GREEN;
                    iconUri = OfficialAssets.Graphics.Icons.Voice.Normal;
                    break;
                case VoiceMode.Shout:
                    voiceColor = RadiantUI_Constants.Hero.YELLOW;
                    iconUri = OfficialAssets.Graphics.Icons.Voice.Shout;
                    break;
                case VoiceMode.Broadcast:
                    voiceColor = RadiantUI_Constants.Hero.CYAN;
                    iconUri = OfficialAssets.Graphics.Icons.Voice.Broadcast;
                    break;
                default: 
                    voiceColor = colorX.Gray;
                    iconUri = new Uri(""); 
                    break;
            }
            staticTexture2D.URL.Value = iconUri;
            sprite.Texture.Target = staticTexture2D;
            ____iconImage.Target.Sprite.Target = sprite;
            ContextMenuItem.UpdateColor(____innerCircleButton, ref voiceColor, highlight: false);
        }
    }
}