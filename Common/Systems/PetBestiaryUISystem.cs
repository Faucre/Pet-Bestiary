using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PetBestiary.UI;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PetBestiary.Common.Systems;

public sealed class PetBestiaryUISystem : ModSystem
{
    private static PetBestiaryUISystem instance;

    internal static bool Visible { get; private set; }

    private UserInterface userInterface;
    private PetBestiaryUIState uiState;

    public override void Load()
    {
        instance = this;
        if (Main.dedServ)
        {
            return;
        }

        uiState = new PetBestiaryUIState();
        uiState.Activate();
        userInterface = new UserInterface();
    }

    public override void Unload()
    {
        userInterface = null;
        uiState = null;
        Visible = false;
        instance = null;
    }

    public static void Close()
    {
        instance?.uiState?.DismissTransientUi();
        Visible = false;
    }

    public static void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            Main.playerInventory = true;
            SoundEngine.PlaySound(SoundID.MenuOpen);
        }
        else
        {
            instance?.uiState?.DismissTransientUi();
            SoundEngine.PlaySound(SoundID.MenuClose);
        }
    }

    public override void PostUpdateInput()
    {
        if (PetBestiary.ToggleBestiaryKeybind != null && PetBestiary.ToggleBestiaryKeybind.JustPressed)
        {
            Toggle();
        }
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (Visible && userInterface != null)
        {
            userInterface.SetState(uiState);
            userInterface.Update(gameTime);
        }
        else
        {
            userInterface?.SetState(null);
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (mouseTextIndex == -1)
        {
            return;
        }

        layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
            "PetBestiary: Inventory Toggle Button",
            DrawInventoryToggleButton,
            InterfaceScaleType.UI));

        if (Visible && userInterface != null)
        {
            layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                "PetBestiary: Pet Bestiary",
                delegate
                {
                    userInterface.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
                    return true;
                },
                InterfaceScaleType.UI));
        }
    }

    private bool DrawInventoryToggleButton()
    {
        if (Main.dedServ || !Main.playerInventory)
        {
            return true;
        }

        Rectangle bounds = GetInventoryToggleButtonBounds();
        bool hovering = bounds.Contains(Main.MouseScreen.ToPoint());
        if (hovering)
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.instance.MouseText("Pet Bestiary");

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                Main.mouseLeftRelease = false;
                Toggle();
            }
        }

        DrawInventoryToggleButton(Main.spriteBatch, bounds, hovering);
        return true;
    }

    private static Rectangle GetInventoryToggleButtonBounds()
    {
        int slotSize = (int)(52f * Main.inventoryScale);
        // Vanilla draws the trash slot below the right side of the inventory/coin area.
        // Keep this button in that lower control strip instead of anchoring it to the
        // main inventory grid, otherwise it lands in the middle of the slots.
        int x = 10 + slotSize * 13;
        int y = 20 + (int)(slotSize * 7.85f);
        return new Rectangle(x, y, slotSize, slotSize);
    }

    private static void DrawInventoryToggleButton(SpriteBatch spriteBatch, Rectangle bounds, bool hovering)
    {
        Color backColor = Visible
            ? new Color(255, 226, 90)
            : hovering
                ? Color.White
                : Main.inventoryBack;

        spriteBatch.Draw(TextureAssets.InventoryBack.Value, bounds, backColor);

        Main.instance.LoadItem(ItemID.Book);
        Texture2D icon = TextureAssets.Item[ItemID.Book].Value;
        if (icon == null)
        {
            return;
        }

        float iconScale = System.Math.Min((bounds.Width - 14f) / icon.Width, (bounds.Height - 14f) / icon.Height);
        Vector2 iconPosition = bounds.Center.ToVector2();
        spriteBatch.Draw(icon, iconPosition, null, Color.White, 0f, icon.Size() * 0.5f, iconScale, SpriteEffects.None, 0f);
    }
}
