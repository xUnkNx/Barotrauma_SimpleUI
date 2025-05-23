﻿using System;
using System.Reflection;
using Barotrauma;
using Barotrauma.Networking;
using Barotrauma.Extensions;
using HarmonyLib;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using static Barotrauma.EventManager;

namespace SimpleUI {
    class SimpleUIMod : IAssemblyPlugin {
        public Harmony? harmony;
        public void Initialize() {
            harmony = new Harmony("SimpleUI");

            // Hide useless crews active reports button
            harmony.Patch(
                original: AccessTools.Method(typeof(CrewManager), "UpdateReports"),
                postfix: new HarmonyMethod(typeof(SimpleUIMod).GetMethod("CrewManager_UpdateReports_after"))
            );

            // Modifies position of original layout and moves useless elements over screenspace
            harmony.Patch(
                original: AccessTools.Method(typeof(HUDLayoutSettings), "CreateAreas"),
                postfix: new HarmonyMethod(typeof(SimpleUIMod).GetMethod("HUDLayoutSettings_CreateAreas_after"))
            );

            // Draw player health and afflictions over players characted in-game
            harmony.Patch(
                original: AccessTools.Method(typeof(Character), "DrawFront"),
                postfix: new HarmonyMethod(typeof(SimpleUIMod).GetMethod("Character_DrawHUD_after"))
            );

            // Move inventory to the left side of screen by game layout based HUD drawing
            harmony.Patch(
                original: AccessTools.PropertySetter(typeof(CharacterInventory), "CurrentLayout"),
                prefix: new HarmonyMethod(typeof(SimpleUIMod).GetMethod("CharacterInventory_CurrentLayout_setter"))
            );

            // Calculate hands slot positions based on left and right layout
            harmony.Patch(
                original: AccessTools.Method(typeof(CharacterInventory), "SetSlotPositions"),
                postfix: new HarmonyMethod(typeof(SimpleUIMod).GetMethod("CharacterInventory_SetSlotPositions_after"))
            );

            // Append slot positions
            harmony.Patch(
                original: AccessTools.Method(typeof(CharacterInventory), "CreateSlots"),
                postfix: new HarmonyMethod(typeof(SimpleUIMod).GetMethod("CharacterInventory_CreateSlots_after"))
            );

            // Fix chat right disposition character health window overlap
            harmony.Patch(
                original: AccessTools.Method(typeof(CharacterHealth), "UpdateAlignment"),
                postfix: new HarmonyMethod(typeof(SimpleUIMod).GetMethod("CharacterHealth_UpdateAlignment_after"))
            );

            // Replaces original chatbox code to make messages more compact
            harmony.Patch(
                original: AccessTools.Method(typeof(ChatBox), "AddMessage"),
                prefix: new HarmonyMethod(typeof(SimpleUIMod).GetMethod("ChatBox_AddMessage_replace"))
            );
        }


        public static void CrewManager_UpdateReports_after(CrewManager __instance) {
            // Disable crewmates report buttons
            __instance.ReportButtonFrame.Visible = false;
        }

        public static void HUDLayoutSettings_CreateAreas_after() {
            // Modify position of default GUI game elements
            int chatBoxWidth = (int) (475 * GUI.Scale * GUI.AspectRatioAdjustment);
            int chatBoxHeight = (int) Math.Max(GameMain.GraphicsHeight * 0.25f, 150);

            HUDLayoutSettings.InventoryAreaLower = new Rectangle(HUDLayoutSettings.Padding, HUDLayoutSettings.inventoryTopY, GameMain.GraphicsWidth - chatBoxWidth + HUDLayoutSettings.Padding, GameMain.GraphicsHeight - HUDLayoutSettings.inventoryTopY);

            HUDLayoutSettings.ChatBoxArea = new Rectangle(GameMain.GraphicsWidth - chatBoxWidth + HUDLayoutSettings.Padding * 3, GameMain.GraphicsHeight - HUDLayoutSettings.Padding - chatBoxHeight, chatBoxWidth, chatBoxHeight);

            int infoAreaWidth = (int) (142 * GUI.Scale);
            int infoAreaHeight = (int) (98 * GUI.Scale);
            HUDLayoutSettings.BottomRightInfoArea = new Rectangle(GameMain.GraphicsWidth, GameMain.GraphicsHeight, 0, 0);
            HUDLayoutSettings.PortraitArea = new Rectangle(GameMain.GraphicsWidth, GameMain.GraphicsHeight, 0, 0);

            int afflictionAreaHeight = (int) (50 * GUI.Scale);
            int healthBarWidth = infoAreaWidth;
            int healthBarHeight = (int) (50f * GUI.Scale);

            var healthBarChildStyles = GUIStyle.GetComponentStyle("CharacterHealthBar")?.ChildStyles;
            if (healthBarChildStyles != null && healthBarChildStyles.TryGetValue("GUIFrame".ToIdentifier(), out var style)) {
                if (style.Sprites.TryGetValue(GUIComponent.ComponentState.None, out var uiSprites) && uiSprites.FirstOrDefault() is { } uiSprite) {
                    // The default health bar uses a sliced sprite so let's make sure the health bar area is calculated accordingly
                    healthBarWidth += (int) (uiSprite.NonSliceSize.X * Math.Min(GUI.Scale, 1f));
                }
            }

            HUDLayoutSettings.HealthBarArea = new Rectangle(GameMain.GraphicsWidth, GameMain.GraphicsHeight, 0, 0);
            HUDLayoutSettings.HealthBarAfflictionArea = new Rectangle(GameMain.GraphicsWidth * 2, GameMain.GraphicsHeight, 0, 0);

            // If need to return back bars
            // HUDLayoutSettings.HealthBarArea = new Rectangle(GameMain.GraphicsWidth - HUDLayoutSettings.Padding * 2 - healthBarWidth + (int) Math.Floor(1 / GUI.Scale), HUDLayoutSettings.ChatBoxArea.Y - healthBarHeight - HUDLayoutSettings.Padding * 5, healthBarWidth, healthBarHeight);
            // HUDLayoutSettings.HealthBarAfflictionArea = new Rectangle(HUDLayoutSettings.HealthBarArea.X, HUDLayoutSettings.HealthBarArea.Y - HUDLayoutSettings.Padding - afflictionAreaHeight, HUDLayoutSettings.HealthBarArea.Width, afflictionAreaHeight);
        }

        public static float dangerScale = 0.15f;

        public static void Character_DrawHUD_after(Character __instance, SpriteBatch spriteBatch, Camera cam) {
            if (Character.controlled == __instance) {
                Vector2 pos = __instance.DrawPosition;

                // Show players afflictions
                Vector2 afflictionsBasePos = new Vector2(pos.X, -pos.Y - __instance.hudInfoHeight - 20);
                CharacterHealth health = __instance.CharacterHealth;
                List<Affliction> afflictions = health.statusIcons;
                bool isEven = afflictions.Count % 2 == 0;
                int i = -(int) Math.Ceiling((decimal) afflictions.Count / 2);
                int k = 0;
                bool inDanger = false;
                foreach (Affliction affliction in afflictions) {
                    AfflictionPrefab afflictionPrefab = affliction.Prefab;
                    Sprite icon = afflictionPrefab.Icon;
                    float scale = 0.2f;
                    // Simple animation when affliction is dangerous
                    if (affliction.DamagePerSecond > 1.0f) {
                        scale = dangerScale;
                        inDanger = true;
                    }
                    icon.Draw(
                        spriteBatch,
                        afflictionsBasePos + new Vector2(icon.size.X * (i + k + (isEven ? 0 : 0.5f)) * 0.2f, 0),
                        CharacterHealth.GetAfflictionIconColor(afflictionPrefab, affliction.Strength),
                        0,
                        scale
                    );
                    k++;
                }
                if (inDanger) {
                    // Not great cuz based on framerate
                    dangerScale += 0.001f;
                    if (dangerScale > 0.2f) {
                        dangerScale = 0.15f;
                    }
                }

                // Show players health
                float vitality = health.DisplayedVitality / __instance.MaxVitality;
                if (vitality < 0.98f && __instance.hudInfoVisible) {
                    Vector2 healthBarPos = new Vector2(pos.X - 50, pos.Y - __instance.hudInfoHeight - 40);
                    GUI.DrawProgressBar(
                        spriteBatch,
                        healthBarPos,
                        new Vector2(100.0f, 10.0f),
                        vitality,
                        Color.Lerp(GUIStyle.Red, GUIStyle.Green, (float) Math.Pow(vitality, 2f)),
                        new Color(0.5f, 0.57f, 0.6f, 1.0f)
                    );
                }
            }
        }

        public static void CharacterInventory_CurrentLayout_setter(ref CharacterInventory.Layout value) {
            // Move inventory to left of the screen by game layout system
            if (value == CharacterInventory.Layout.Default) {
                value = CharacterInventory.Layout.Left;
            }
        }

        public static void CharacterInventory_SetSlotPositions_after(CharacterInventory __instance, CharacterInventory.Layout layout) {
            // Fix hands slots on left/right layout when active slots is lower than 7
            VisualSlot[] visualSlots = __instance.visualSlots;
            if (visualSlots.None()) { return; }
            Vector2[] SlotPositions = __instance.SlotPositions;
            InvSlotType[] SlotTypes = __instance.SlotTypes;
            int spacing = GUI.IntScale(5);
            int bottomOffset = CharacterInventory.SlotSize.Y + spacing * 2 + Inventory.ContainedIndicatorHeight;
            int personalSlotY = GameMain.GraphicsHeight - (bottomOffset + spacing) * 2 - (int) (Inventory.UnequippedIndicator.size.Y * Inventory.UIScale);
            switch (layout) {
                case CharacterInventory.Layout.Left: {
                        int x = HUDLayoutSettings.InventoryAreaLower.X;
                        float y = GameMain.GraphicsHeight - bottomOffset;
                        if (!GUI.IsUltrawide && GUI.IsHUDScaled) {
                            const float margin = 100;
                            x -= HUDLayoutSettings.ChatBoxArea.Width - (int) margin;
                        }
                        int handSlotX = x + visualSlots[0].Rect.Width * 7 + spacing * 10;

                        for (int i = 0; i < SlotPositions.Length; i++) {
                            if (SlotTypes[i] == InvSlotType.RightHand || SlotTypes[i] == InvSlotType.LeftHand) {
                                bool rightSlot = SlotTypes[i] == InvSlotType.RightHand;
                                SlotPositions[i] = new Vector2(rightSlot ? handSlotX : handSlotX - visualSlots[0].Rect.Width - spacing, personalSlotY);
                                UpdateSlotPosition(__instance, i, SlotPositions[i]);
                            }
                        }
                    }
                    break;
                case CharacterInventory.Layout.Right: {
                        int x = HUDLayoutSettings.InventoryAreaLower.Right;
                        int handSlotX = x - visualSlots[0].Rect.Width * 8 - spacing * 11;
                        for (int i = 0; i < SlotPositions.Length; i++) {
                            if (SlotTypes[i] == InvSlotType.RightHand || SlotTypes[i] == InvSlotType.LeftHand) {
                                SlotPositions[i] = new Vector2(handSlotX, personalSlotY);
                                UpdateSlotPosition(__instance, i, SlotPositions[i]);
                                handSlotX += visualSlots[i].Rect.Width + spacing;
                            }
                        }
                    }
                    break;
            }
        }

        public static void UpdateSlotPosition(CharacterInventory __instance, int index, Vector2 position) {
            // Since positions are updated only in CreateSlots function, do it manually after SetSlotPositions to overwrite the result
            float multiplier = Inventory.UIScale * GUI.AspectRatioAdjustment;
            Sprite slotSprite = CharacterInventory.SlotSpriteSmall;
            Rectangle slotRect = new Rectangle(
                (int) position.X,
                (int) position.Y,
                (int) (slotSprite.size.X * multiplier), (int) (slotSprite.size.Y * multiplier));

            __instance.visualSlots[index] = new VisualSlot(slotRect);
        }

        public static void CharacterInventory_CreateSlots_after(CharacterInventory __instance) {
            // In case when called without SetSlotPositions
            CharacterInventory_SetSlotPositions_after(__instance, __instance.CurrentLayout);
        }

        public static void CharacterHealth_UpdateAlignment_after(CharacterHealth __instance) {
            GUIFrame healthWindow = __instance.healthWindow;
            Point screenResolution = __instance.screenResolution;

            switch (__instance.alignment) {
                case Alignment.Right:
                    healthWindow.RectTransform.AbsoluteOffset = new Point(HUDLayoutSettings.Padding, screenResolution.Y - HUDLayoutSettings.ChatBoxArea.Y + HUDLayoutSettings.Padding * 4);
                    break;
            }
        }

        public static bool ChatBox_AddMessage_replace(ChatBox __instance, ref ChatMessage message) {
            // Its just redefine of base AddMessage function, because its hard to modify original stack (parenting there is based on RectTransform)
            GUIListBox chatBox = __instance.chatBox;
            if (GameMain.IsSingleplayer) {
                var should = GameMain.LuaCs.Hook.Call<bool?>("chatMessage", message.Text, message.SenderClient, message.Type, message);
                if (should != null && should.Value) { return false; }
            }

            while (chatBox.Content.CountChildren > 60) {
                chatBox.RemoveChild(chatBox.Content.Children.First());
            }

            float prevSize = chatBox.BarSize;

            string senderName = "";
            if (!string.IsNullOrWhiteSpace(message.SenderName)) {
                senderName = (message.Type == ChatMessageType.Private ? "[PM] " : "") + message.SenderName;
            }
            string timeStamp = ChatMessage.GetTimeStamp();
            string translatedText = message.TranslatedText;
            string displayedText = translatedText;

            Color senderColor = Color.White;
            if (message.SenderCharacter?.Info?.Job != null) {
                senderColor = Color.Lerp(message.SenderCharacter.Info.Job.Prefab.UIColor, Color.White, 0.25f);
            }

            var msgHolder = new GUIFrame(new RectTransform(new Vector2(0.95f, 0.0f), chatBox.Content.RectTransform, Anchor.TopCenter), style: null,
                    color: ((chatBox.Content.CountChildren % 2) == 0) ? Color.Transparent : Color.Black * 0.1f);

            GUITextBlock senderNameTimestamp = new GUITextBlock(new RectTransform(new Vector2(0.98f, 0.0f), msgHolder.RectTransform) { AbsoluteOffset = new Point((int) (5 * GUI.Scale), 0) },
                timeStamp, textColor: Color.LightGray, font: GUIStyle.SmallFont, textAlignment: Alignment.TopLeft, style: null) {
                CanBeFocused = true
            };
            if (!string.IsNullOrEmpty(senderName)) {
                senderName += ": ";
                var senderNameBlock = new GUIButton(new RectTransform(new Vector2(0.8f, 1.0f), senderNameTimestamp.RectTransform) { AbsoluteOffset = new Point((int) (senderNameTimestamp.TextSize.X), 0) },
                    senderName, textAlignment: Alignment.TopLeft, style: null, color: Color.Transparent) {
                    TextBlock =
                    {
                        Padding = Vector4.Zero
                    },
                    Font = GUIStyle.SmallFont,
                    CanBeFocused = true,
                    ForceUpperCase = ForceUpperCase.No,
                    UserData = message.SenderClient,
                    PlaySoundOnSelect = false,
                    OnClicked = (_, o) => {
                        if (!(o is Client client)) { return false; }
                        if (GameMain.NetLobbyScreen != null) {
                            GameMain.NetLobbyScreen.SelectPlayer(client);
                            SoundPlayer.PlayUISound(GUISoundType.Select);
                        }
                        return true;
                    },
                    OnSecondaryClicked = (_, o) => {
                        if (!(o is Client client)) { return false; }
                        NetLobbyScreen.CreateModerationContextMenu(client);
                        return true;
                    },
                    Text = senderName
                };

                senderNameBlock.RectTransform.NonScaledSize = senderNameBlock.TextBlock.TextSize.ToPoint();
                senderNameBlock.TextBlock.OverrideTextColor(senderColor);
                if (senderNameBlock.UserData != null) {
                    senderNameBlock.TextBlock.HoverTextColor = Color.White;
                }

            }

            // There we're pushing in front of message space characters to fake player nick name and timestamp text
            // This is because RichText supports only rectangle form and we don't like to split text by our own
            Vector2 approx = GUIStyle.SmallFont.MeasureChar(' ');
            Vector2 req = GUIStyle.SmallFont.MeasureString(timeStamp + senderName);
            displayedText = new string(' ', (int) Math.Max(0, Math.Ceiling((float) (req.X / approx.X)) - 1)) + displayedText;

            var msgText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), msgHolder.RectTransform) { AbsoluteOffset = new Point((int) (10 * GUI.Scale), 0) },
                RichString.Rich(displayedText), textColor: message.Color, font: GUIStyle.SmallFont, textAlignment: Alignment.TopLeft, style: null, wrap: true,
                color: ((chatBox.Content.CountChildren % 2) == 0) ? Color.Transparent : Color.Black * 0.1f) {
                UserData = message.SenderName,
                CanBeFocused = false
            };
            msgText.CalculateHeightFromText();
            if (msgText.RichTextData != null) {
                foreach (var data in msgText.RichTextData) {
                    var clickableArea = new GUITextBlock.ClickableArea() {
                        Data = data
                    };
                    if (GameMain.NetLobbyScreen != null && GameMain.NetworkMember != null) {
                        clickableArea.OnClick = GameMain.NetLobbyScreen.SelectPlayer;
                        clickableArea.OnSecondaryClick = GameMain.NetLobbyScreen.ShowPlayerContextMenu;
                    }
                    msgText.ClickableAreas.Add(clickableArea);
                }
            }

            if (message is OrderChatMessage orderChatMsg &&
                Character.Controlled != null &&
                orderChatMsg.TargetCharacter == Character.Controlled) {
                msgHolder.Flash(Color.OrangeRed * 0.6f, flashDuration: 5.0f);
            }
            else {
                msgHolder.Flash(Color.Yellow * 0.6f);
            }
            msgHolder.RectTransform.SizeChanged += Recalculate;
            Recalculate();
            void Recalculate() {
                msgHolder.RectTransform.SizeChanged -= Recalculate;
                //resize the holder to match the size of the message and add some spacing
                msgText.RectTransform.MaxSize = new Point(msgHolder.Rect.Width - msgText.RectTransform.AbsoluteOffset.X, int.MaxValue);
                if (senderNameTimestamp != null) {
                    senderNameTimestamp.RectTransform.MaxSize = new Point(msgHolder.Rect.Width - senderNameTimestamp.RectTransform.AbsoluteOffset.X, int.MaxValue);
                }
                msgHolder.Children.ForEach(c => (c as GUITextBlock)?.CalculateHeightFromText());
                msgHolder.RectTransform.Resize(new Point(msgHolder.Rect.Width, msgText.Rect.Height), resizeChildren: false);
                msgHolder.RectTransform.SizeChanged += Recalculate;
                chatBox.RecalculateChildren();
                chatBox.UpdateScrollBarSize();
            }

            CoroutineManager.StartCoroutine(__instance.UpdateMessageAnimation(msgHolder, 0.5f));

            chatBox.UpdateScrollBarSize();

            if (chatBox.ScrollBar.Visible && chatBox.ScrollBar.BarScroll < 1f) {
                __instance.showNewMessagesButton.Visible = true;
            }

            if (message.Type == ChatMessageType.Server && message.ChangeType != PlayerConnectionChangeType.None) {
                TabMenu.StorePlayerConnectionChangeMessage(message);
            }

            if (!__instance.ToggleOpen) {
                var popupMsg = new GUIFrame(new RectTransform(Vector2.One, __instance.GUIFrame.RectTransform), style: "GUIToolTip") {
                    UserData = 0.0f,
                    CanBeFocused = false
                };
                var content = new GUILayoutGroup(new RectTransform(new Vector2(0.95f, 0.9f), popupMsg.RectTransform, Anchor.Center));
                Vector2 senderTextSize = Vector2.Zero;
                if (!string.IsNullOrEmpty(senderName)) {
                    var senderText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), content.RectTransform),
                        senderName, textColor: senderColor, style: null, font: GUIStyle.SmallFont) {
                        CanBeFocused = false
                    };
                    senderTextSize = senderText.Font.MeasureString(senderText.WrappedText);
                    senderText.RectTransform.MinSize = new Point(0, senderText.Rect.Height);
                }
                var msgPopupText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), content.RectTransform),
                    RichString.Rich(translatedText), textColor: message.Color, font: GUIStyle.SmallFont, textAlignment: Alignment.BottomLeft, style: null, wrap: true) {
                    CanBeFocused = false
                };
                msgPopupText.RectTransform.MinSize = new Point(0, msgPopupText.Rect.Height);
                Vector2 msgSize = msgPopupText.Font.MeasureString(msgPopupText.WrappedText);
                int textWidth = (int) Math.Max(msgSize.X + msgPopupText.Padding.X + msgPopupText.Padding.Z, senderTextSize.X) + 10;
                popupMsg.RectTransform.Resize(new Point((int) (textWidth / content.RectTransform.RelativeSize.X), (int) ((senderTextSize.Y + msgSize.Y) / content.RectTransform.RelativeSize.Y)), resizeChildren: true);
                popupMsg.RectTransform.IsFixedSize = true;
                content.Recalculate();
                __instance.popupMessages.Add(popupMsg);
            }

            if ((prevSize == 1.0f && chatBox.BarScroll == 0.0f) || (prevSize < 1.0f && chatBox.BarScroll == 1.0f)) { chatBox.BarScroll = 1.0f; }

            GUISoundType soundType = GUISoundType.ChatMessage;
            if (message.Type == ChatMessageType.Radio) {
                soundType = GUISoundType.RadioMessage;
            }
            else if (message.Type == ChatMessageType.Dead) {
                soundType = GUISoundType.DeadMessage;
            }

            SoundPlayer.PlayUISound(soundType);
            return false;
        }

        public void OnLoadCompleted() {
            HUDLayoutSettings_CreateAreas_after();
        }

        public void PreInitPatching() {
        }

        public void Dispose() {
            if (harmony != null) {
                harmony.UnpatchSelf();
            }
        }
    }
}
