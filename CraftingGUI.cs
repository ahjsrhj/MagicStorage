using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using MagicStorage.Components;
using MagicStorage.Sorting;

namespace MagicStorage
{
	public static class CraftingGUI
	{
		private const int padding = 4;
		private const int numColumns = 10;
		private const float inventoryScale = 0.85f;
		private const float recipeScale = 0.7f;

		public static MouseState curMouse;
		public static MouseState oldMouse;
		public static bool MouseClicked
		{
			get
			{
				return curMouse.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released;
			}
		}

		private static UIPanel basePanel = new UIPanel();
		private static float panelTop;
		private static float panelLeft;
		private static float panelWidth;
		private static float panelHeight;

		private static UIElement topBar = new UIElement();
		internal static UISearchBar searchBar = new UISearchBar();
		internal static UIButtonChoice sortButtons;
		private static UIElement stationZone = new UIElement();
		private static UIText stationText = new UIText("Crafting Stations");
		private static UIElement slotZone = new UIElement();

		internal static UIScrollbar scrollBar = new UIScrollbar();
		private static bool scrollBarFocus = false;
		private static int scrollBarFocusMouseStart;
		private static float scrollBarFocusPositionStart;
		private static float scrollBarViewSize = 1f;
		private static float scrollBarMaxViewSize = 2f;

		private static List<Item> items = new List<Item>();
		private static int numRows;
		private static int displayRows;
		private static int hoverSlot = -1;
		private static int slotFocus = -1;
		private static int rightClickTimer = 0;
		private const int startMaxRightClickTimer = 20;
		private static int maxRightClickTimer = startMaxRightClickTimer;

		private static UIElement bottomBar = new UIElement();
		private static UIText capacityText = new UIText("Items");

		public static void Initialize()
		{
			float itemSlotWidth = Main.inventoryBackTexture.Width * inventoryScale;
			float itemSlotHeight = Main.inventoryBackTexture.Height * inventoryScale;

			panelTop = Main.instance.invBottom + 60;
			panelLeft = 20f;
			float innerPanelLeft = panelLeft + basePanel.PaddingLeft;
			float innerPanelWidth = numColumns * (itemSlotWidth + padding) + 20f + padding;
			panelWidth = basePanel.PaddingLeft + innerPanelWidth + basePanel.PaddingRight;
			panelHeight = Main.screenHeight - panelTop - 40f;
			basePanel.Left.Set(panelLeft, 0f);
			basePanel.Top.Set(panelTop, 0f);
			basePanel.Width.Set(panelWidth, 0f);
			basePanel.Height.Set(panelHeight, 0f);
			basePanel.Recalculate();

			topBar.Width.Set(0f, 1f);
			topBar.Height.Set(32f, 0f);
			basePanel.Append(topBar);

			InitSortButtons();
			topBar.Append(sortButtons);

			searchBar.Left.Set(0f, 0.5f);
			searchBar.Width.Set(0f, 0.5f);
			searchBar.Height.Set(0f, 1f);
			topBar.Append(searchBar);

			stationZone.Width.Set(0f, 1f);
			stationZone.Top.Set(40f, 0f);
			stationZone.Height.Set(60f, 0f);
			basePanel.Append(stationZone);
			stationZone.Append(stationText);

			slotZone.Width.Set(0f, 1f);
			slotZone.Top.Set(100f, 0f);
			slotZone.Height.Set(-140f, 1f);
			basePanel.Append(slotZone);

			numRows = (items.Count + numColumns - 1) / numColumns;
			displayRows = (int)slotZone.GetDimensions().Height / ((int)itemSlotHeight + padding);
			int noDisplayRows = numRows - displayRows;
			if (noDisplayRows < 0)
			{
				noDisplayRows = 0;
			}
			scrollBarMaxViewSize = 1 + noDisplayRows;
			scrollBar.Height.Set(displayRows * (itemSlotHeight + padding), 0f);
			scrollBar.Left.Set(-20f, 1f);
			scrollBar.SetView(scrollBarViewSize, scrollBarMaxViewSize);
			slotZone.Append(scrollBar);

			bottomBar.Width.Set(0f, 1f);
			bottomBar.Height.Set(32f, 0f);
			bottomBar.Top.Set(-32f, 1f);
			basePanel.Append(bottomBar);

			capacityText.Left.Set(6f, 0f);
			capacityText.Top.Set(6f, 0f);
			TEStorageHeart heart = GetHeart();
			int numItems = 0;
			int capacity = 0;
			if (heart != null)
			{
				foreach (TEAbstractStorageUnit abstractStorageUnit in heart.GetStorageUnits())
				{
					if (abstractStorageUnit is TEStorageUnit)
					{
						TEStorageUnit storageUnit = (TEStorageUnit)abstractStorageUnit;
						numItems += storageUnit.NumItems;
						capacity += storageUnit.Capacity;
					}
				}
			}
			capacityText.SetText(numItems + "/" + capacity + " Items");
			bottomBar.Append(capacityText);
		}

		private static void InitSortButtons()
		{
			if (sortButtons == null)
			{
				sortButtons = new UIButtonChoice(new Texture2D[]
				{
					Main.inventorySortTexture[0],
					MagicStorage.Instance.GetTexture("SortID"),
					MagicStorage.Instance.GetTexture("SortName")
				},
				new string[]
				{
					"Default Sorting",
					"Sort By ID",
					"Sort By Name"
				});
			}
		}

		public static void Update(GameTime gameTime)
		{
			oldMouse = StorageGUI.oldMouse;
			curMouse = StorageGUI.curMouse;
			if (Main.playerInventory && Main.player[Main.myPlayer].GetModPlayer<StoragePlayer>(MagicStorage.Instance).ViewingStorage().X >= 0)
			{
				basePanel.Update(gameTime);
				UpdateScrollBar();
			}
			else
			{
				scrollBarFocus = false;
			}
		}

		public static void Draw(TEStorageHeart heart)
		{
			Player player = Main.player[Main.myPlayer];
			StoragePlayer modPlayer = player.GetModPlayer<StoragePlayer>(MagicStorage.Instance);
			Initialize();
			if (Main.mouseX > panelLeft && Main.mouseX < panelLeft + panelWidth && Main.mouseY > panelTop && Main.mouseY < panelTop + panelHeight)
			{
				player.mouseInterface = true;
			}
			basePanel.Draw(Main.spriteBatch);
			float itemSlotWidth = Main.inventoryBackTexture.Width * inventoryScale;
			float itemSlotHeight = Main.inventoryBackTexture.Height * inventoryScale;
			Vector2 slotZonePos = slotZone.GetDimensions().Position();
			float oldScale = Main.inventoryScale;
			Main.inventoryScale = inventoryScale;
			Item[] temp = new Item[11];
			for (int k = 0; k < numColumns * displayRows; k++)
			{
				int index = k + numColumns * (int)Math.Round(scrollBar.ViewPosition);
				Item item = index < items.Count ? items[index] : new Item();
				Vector2 drawPos = slotZonePos + new Vector2((itemSlotWidth + padding) * (k % 10), (itemSlotHeight + padding) * (k / 10));
				temp[10] = item;
				ItemSlot.Draw(Main.spriteBatch, temp, 0, 10, drawPos);
			}
			if (hoverSlot >= 0 && hoverSlot < items.Count)
			{
				Main.toolTip = items[hoverSlot].Clone();
				Main.instance.MouseText(string.Empty);
			}
			sortButtons.DrawText();
			Main.inventoryScale = oldScale;
		}

		private static void UpdateScrollBar()
		{
			if (slotFocus >= 0)
			{
				scrollBarFocus = false;
				return;
			}
			CalculatedStyle dim = scrollBar.GetInnerDimensions();
			Vector2 boxPos = new Vector2(dim.X, dim.Y + dim.Height * (scrollBar.ViewPosition / scrollBarMaxViewSize));
			float boxWidth = 20f;
			float boxHeight = dim.Height * (scrollBarViewSize / scrollBarMaxViewSize);
			if (scrollBarFocus)
			{
				if (curMouse.LeftButton == ButtonState.Released)
				{
					scrollBarFocus = false;
				}
				else
				{
					int difference = curMouse.Y - scrollBarFocusMouseStart;
					scrollBar.ViewPosition = scrollBarFocusPositionStart + (float)difference / boxHeight;
				}
			}
			else if (MouseClicked)
			{
				if (curMouse.X > boxPos.X && curMouse.X < boxPos.X + boxWidth && curMouse.Y > boxPos.Y - 3f && curMouse.Y < boxPos.Y + boxHeight + 4f)
				{
					scrollBarFocus = true;
					scrollBarFocusMouseStart = curMouse.Y;
					scrollBarFocusPositionStart = scrollBar.ViewPosition;
				}
			}
			if (!scrollBarFocus)
			{
				int difference = oldMouse.ScrollWheelValue / 250 - curMouse.ScrollWheelValue / 250;
				scrollBar.ViewPosition += difference;
			}
		}

		private static TEStorageHeart GetHeart()
		{
			Player player = Main.player[Main.myPlayer];
			StoragePlayer modPlayer = player.GetModPlayer<StoragePlayer>(MagicStorage.Instance);
			Point16 pos = modPlayer.ViewingStorage();
			if (pos.X < 0 || pos.Y < 0)
			{
				return null;
			}
			Tile tile = Main.tile[pos.X, pos.Y];
			if (tile == null)
			{
				return null;
			}
			int tileType = tile.type;
			ModTile modTile = TileLoader.GetTile(tileType);
			if (modTile == null || !(modTile is StorageAccess))
			{
				return null;
			}
			return ((StorageAccess)modTile).GetHeart(pos.X, pos.Y);
		}

		private static TECraftingAccess GetCraftingEntity()
		{
			Player player = Main.player[Main.myPlayer];
			StoragePlayer modPlayer = player.GetModPlayer<StoragePlayer>(MagicStorage.Instance);
			Point16 pos = modPlayer.ViewingStorage();
			if (pos.X < 0 || pos.Y < 0 || !TileEntity.ByPosition.ContainsKey(pos))
			{
				return null;
			}
			return TileEntity.ByPosition[pos] as TECraftingAccess;
		}

		private static Item[] GetCraftingStations()
		{
			TECraftingAccess ent = GetCraftingEntity();
			return ent == null ? null : ent.stations;
		}

		public static void RefreshItems()
		{
			items.Clear();
			TEStorageHeart heart = GetHeart();
			if (heart == null)
			{
				return;
			}
			InitSortButtons();
			SortMode sortMode;
			switch (sortButtons.Choice)
			{
			case 0:
				sortMode = SortMode.Default;
				break;
			case 1:
				sortMode = SortMode.Id;
				break;
			case 2:
				sortMode = SortMode.Name;
				break;
			case 3:
				sortMode = SortMode.Quantity;
				break;
			default:
				sortMode = SortMode.Default;
				break;
			}
			items.AddRange(ItemSorter.SortAndFilter(heart.GetStoredItems(), sortMode, searchBar.Text));
		}
	}
}