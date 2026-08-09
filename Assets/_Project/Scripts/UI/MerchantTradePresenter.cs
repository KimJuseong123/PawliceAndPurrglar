using System.Collections.Generic;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// MERCHANT-001..003. The raccoon's ledger: what the thief is carrying on the
    /// left, what they have decided to sell on the right, and the total.
    ///
    /// A ledger rather than a single instant sale because the bag now holds
    /// several pieces. "Press E and the thing in your hands goes" is still there
    /// and still the quick path, but it cannot express "sell the coins, keep the
    /// ring" — and with a bag that fills up, choosing what to keep is the
    /// interesting decision at the merchant.
    ///
    /// Built in code at runtime rather than in the HUD prefab, deliberately. Every
    /// panel here needs a generated rounded sprite and a click handler, and both
    /// of those are exactly what does *not* survive an editor script writing a
    /// prefab: the sprite is not an asset (so it serializes as null, and a null
    /// sprite is a white box) and a listener added from the editor is
    /// non-persistent (so the button is dead in the build only — <c>ISSUE-017</c>).
    /// Assembling on the machine that draws it makes both problems impossible
    /// rather than merely unlikely.
    ///
    /// Opened and closed with the interact key, from the HUD's own local key
    /// handler. It does not suppress gameplay input: the raccoon's pitch is the one
    /// spot on the map the police most want to stand, so a modal window would trap
    /// the thief there.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MerchantTradePresenter : MonoBehaviour
    {
        private const int GridColumns = 5;
        private const int CellCount = 25;
        private const int QuickSlotCount = 4;
        private const int RowCount = 6;

        /// <summary>
        /// Which side of the counter the local player is standing on.
        ///
        /// One window rather than two screens. The raccoon is one place with one
        /// look, and the officer buying a glue trap is doing the same thing the
        /// thief is doing in the other direction — reading a price and pressing
        /// once. Two presenters would be two places for the header, the chrome and
        /// the money badge to drift apart.
        /// </summary>
        public enum Mode
        {
            /// <summary>The thief, selling treasure out of the bag.</summary>
            Sell = 0,

            /// <summary>The officer, buying equipment.</summary>
            Buy = 1
        }

        private sealed class LedgerRow
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text Name;
            public TMP_Text Count;
            public TMP_Text Price;
            public Button Action;
            public TMP_Text ActionLabel;
            public LootDefinition Definition;
            public ThrowableKind Kind;
        }

        private readonly List<LedgerRow> _rows = new();
        private readonly List<LootDefinition> _basketKinds = new();
        private readonly Dictionary<LootDefinition, int> _basket = new();

        private GameObject _window;
        private InventorySlotView[] _cells = System.Array.Empty<InventorySlotView>();
        private TMP_Text _totalLabel;
        private TMP_Text _emptyHint;
        private CurrencyBadgeView _badge;
        private Button _sellButton;
        private Button _sellAllButton;
        private Button _cancelButton;

        private LootCarrier _carrier;
        private ThiefLootWallet _wallet;
        private LootConfig _lootConfig;

        /// <summary>
        /// The four prop slots, so the grid is the bag the player just looked at.
        ///
        /// They cannot be sold — the raccoon buys treasure, and a prop has no
        /// price anybody has decided on (`GAP-004`). They are still drawn, because
        /// a grid that silently omits four occupied cells does not read as "these
        /// are not for sale", it reads as the screen having lost them.
        /// </summary>
        private ToolCarrier _propCarrier;

        /// <summary>Seconds left on the "this cannot be sold" line.</summary>
        private float _noticeSeconds;
        private TMP_Text _gridHint;
        private string _gridHintText = string.Empty;

        private Mode _mode = Mode.Sell;
        private bool _built;
        private Mode _builtMode = Mode.Sell;
        private PoliceWallet _policeWallet;

        /// <summary>
        /// What the raccoon has for sale, read once per opening.
        ///
        /// Read off the <see cref="PoliceSupplyCounter"/>s already standing in the
        /// supermarket rather than written here. The officer's economy exists and
        /// has prices somebody balanced (glue trap 60, sensor 90, tuna 40); a
        /// second list would be a second place to rebalance and the two would part
        /// company on the first tuning pass.
        /// </summary>
        private readonly List<(ThrowableKind Kind, int Price)> _stock = new();

        public bool IsOpen => _window != null && _window.activeSelf;

        /// <summary>
        /// Opens the ledger for one carrier. Re-opening while open is a no-op, so
        /// standing in the raccoon's zone does not rebuild the basket every frame.
        /// </summary>
        public void Open(
            LootCarrier carrier,
            ThiefLootWallet wallet,
            LootConfig lootConfig,
            ToolCarrier propCarrier = null)
        {
            _mode = Mode.Sell;
            _carrier = carrier;
            _wallet = wallet;
            _lootConfig = lootConfig;
            _propCarrier = propCarrier;
            EnsureBuilt();
            if (IsOpen)
            {
                return;
            }

            _basket.Clear();
            _basketKinds.Clear();
            _window.SetActive(true);
            SetToolUseSuppressed(true);
            Refresh();
        }

        /// <summary>
        /// Opens the raccoon's stall for the officer, who buys rather than sells.
        ///
        /// The officer's props are the answer to a thief who never comes out into
        /// the open: a trap punishes running the same route twice and a sensor
        /// punishes running past the same corner twice. Those were already on sale
        /// at the supermarket counters; putting them at the raccoon too means the
        /// two players share one landmark and can meet at it, which is worth more
        /// than a second shop nobody walks to.
        /// </summary>
        public void OpenShop(
            ToolCarrier propCarrier,
            PoliceWallet wallet)
        {
            _mode = Mode.Buy;
            _propCarrier = propCarrier;
            _policeWallet = wallet;
            _carrier = null;
            _wallet = null;
            EnsureBuilt();
            if (IsOpen)
            {
                return;
            }

            ResolveStock();
            _window.SetActive(true);
            SetToolUseSuppressed(true);
            Refresh();
        }

        /// <summary>
        /// Fills the stall from the counters standing in the world.
        ///
        /// Falls back to the three kinds and prices the counters are built with, so
        /// a scene that has not placed them yet shows a stall with goods in it
        /// rather than an empty shelf that reads as the shop being broken.
        /// </summary>
        private void ResolveStock()
        {
            _stock.Clear();
            foreach (PoliceSupplyCounter counter in
                FindObjectsByType<PoliceSupplyCounter>(FindObjectsSortMode.None))
            {
                if (counter == null)
                {
                    continue;
                }

                bool known = false;
                foreach ((ThrowableKind kind, int _) in _stock)
                {
                    if (kind == counter.Kind)
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                {
                    _stock.Add((counter.Kind, counter.Price));
                }
            }

            if (_stock.Count == 0)
            {
                _stock.Add((ThrowableKind.GlueTrap, 60));
                _stock.Add((ThrowableKind.SensorLight, 90));
                _stock.Add((ThrowableKind.TunaCan, 40));
            }

            _stock.Sort((left, right) => left.Price.CompareTo(right.Price));
        }

        public void Close()
        {
            if (_window != null)
            {
                _window.SetActive(false);
            }

            _basket.Clear();
            _basketKinds.Clear();
            SetToolUseSuppressed(false);
        }

        /// <summary>
        /// Lets the thrower know a shop is in the way of the mouse.
        ///
        /// Latched per presenter so closing this window cannot clear a flag some
        /// other screen set, and released in <c>OnDisable</c> as well: a scene
        /// change while the ledger is open would otherwise leave the left button
        /// dead for the rest of the session.
        /// </summary>
        private bool _suppressedToolUse;

        private void SetToolUseSuppressed(bool suppressed)
        {
            if (_suppressedToolUse == suppressed)
            {
                return;
            }

            _suppressedToolUse = suppressed;
            GameplayInputRouter.SetToolUseSuppressed(suppressed);
        }

        private void OnDisable()
        {
            SetToolUseSuppressed(false);
        }

        private void Update()
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        /// <summary>
        /// Redraws both sides.
        ///
        /// Polled rather than event-driven because the bag can change without the
        /// screen touching it — a piece confiscated, a cat delivering, the host
        /// replicating a sale the moment it lands. A ledger that only updated when
        /// clicked would offer the player a gemstone they no longer have.
        /// </summary>
        private void Refresh()
        {
            if (_mode == Mode.Buy)
            {
                RefreshShop();
                return;
            }

            LootBag bag = _carrier != null ? _carrier.Cells : null;
            for (int index = 0; index < _cells.Length; index++)
            {
                if (index < QuickSlotCount)
                {
                    BindPropCell(index);
                    continue;
                }

                int cell = index - QuickSlotCount;
                LootDefinition definition = null;
                int count = 0;
                bool inBag = bag != null
                    && bag.TryGetCell(cell, out definition, out count);
                int reserved = inBag && definition != null
                    ? BasketCount(definition)
                    : 0;
                int available = Mathf.Max(0, count - reserved);

                _cells[index]?.Bind(new InventorySlotViewModel(
                    (index + 1).ToString(),
                    inBag ? ResolveIcon(definition) : null,
                    available,
                    false,
                    !inBag || available <= 0,
                    inBag && ResolveIcon(definition) == null
                        ? GlyphFor(definition)
                        : string.Empty,
                    string.Empty,
                    inBag ? UnitPrice(definition) : 0,
                    inBag ? CoinSprite : null));
            }

            TickNotice();
            PruneBasket(bag);

            int total = 0;
            for (int row = 0; row < _rows.Count; row++)
            {
                LedgerRow view = _rows[row];
                if (row >= _basketKinds.Count)
                {
                    view.Definition = null;
                    if (view.Root.activeSelf)
                    {
                        view.Root.SetActive(false);
                    }

                    continue;
                }

                LootDefinition definition = _basketKinds[row];
                int count = BasketCount(definition);
                int price = UnitPrice(definition) * count;
                total += price;
                view.Definition = definition;
                if (!view.Root.activeSelf)
                {
                    view.Root.SetActive(true);
                }

                view.Name.text = definition.DisplayName;
                view.Count.text = $"x{count}";
                view.Price.text = price.ToString("N0");
                Sprite icon = ResolveIcon(definition);
                view.Icon.sprite = icon;
                view.Icon.enabled = icon != null;
            }

            if (_totalLabel != null)
            {
                _totalLabel.text = total.ToString("N0");
            }

            if (_emptyHint != null)
            {
                bool empty = _basketKinds.Count == 0;
                if (_emptyHint.gameObject.activeSelf != empty)
                {
                    _emptyHint.gameObject.SetActive(empty);
                }
            }

            _badge?.Bind(_wallet != null ? _wallet.SoldAmount : 0);
        }

        /// <summary>
        /// Redraws the stall: what the officer holds on the left, the goods and
        /// their prices on the right.
        /// </summary>
        private void RefreshShop()
        {
            for (int index = 0; index < _cells.Length; index++)
            {
                if (index < QuickSlotCount)
                {
                    BindOwnedPropCell(index);
                    continue;
                }

                // The officer has no loot bag, so the rest of the grid is empty
                // and says so by being empty. Twenty-one cells that can never
                // hold anything would be twenty-one questions.
                _cells[index]?.Bind(new InventorySlotViewModel(
                    (index + 1).ToString(),
                    null,
                    0,
                    false,
                    true));
            }

            int purse = _policeWallet != null ? _policeWallet.Amount : 0;
            for (int row = 0; row < _rows.Count; row++)
            {
                LedgerRow view = _rows[row];
                if (row >= _stock.Count)
                {
                    if (view.Root.activeSelf)
                    {
                        view.Root.SetActive(false);
                    }

                    continue;
                }

                (ThrowableKind kind, int price) = _stock[row];
                view.Kind = kind;
                if (!view.Root.activeSelf)
                {
                    view.Root.SetActive(true);
                }

                view.Name.text = ThrowableCatalog.GetDisplayName(kind);
                view.Count.text = $"보유 {CountOwned(kind)}";
                view.Price.text = price.ToString("N0");
                Sprite icon = PropIcon(kind);
                view.Icon.sprite = icon;
                view.Icon.enabled = icon != null;

                // Dimmed when it cannot be bought, and the reason is said on the
                // press rather than guessed from the dimming. Two reasons look
                // identical from outside: no money, and no room.
                bool affordable = purse >= price;
                if (view.ActionLabel != null)
                {
                    view.ActionLabel.color = affordable
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0.45f);
                }
            }

            if (_totalLabel != null)
            {
                _totalLabel.text = purse.ToString("N0");
            }

            if (_emptyHint != null && _emptyHint.gameObject.activeSelf)
            {
                _emptyHint.gameObject.SetActive(false);
            }

            _badge?.Bind(purse);
            TickNotice();
        }

        private void BindOwnedPropCell(int index)
        {
            ThrowableKind kind = ThrowableKind.Rock;
            bool hasProp = _propCarrier != null
                && _propCarrier.TryGetSlot(index, out kind)
                && ThrowableCatalog.CanUseInQuickSlot(kind);
            Sprite icon = hasProp ? PropIcon(kind) : null;
            _cells[index]?.Bind(new InventorySlotViewModel(
                (index + 1).ToString(),
                icon,
                hasProp ? _propCarrier.GetSlotQuantity(index) : 0,
                _propCarrier != null && _propCarrier.SelectedSlot == index && hasProp,
                !hasProp,
                hasProp && icon == null ? PropGlyph(kind) : string.Empty));
        }

        private int CountOwned(ThrowableKind kind)
        {
            if (_propCarrier == null)
            {
                return 0;
            }

            int owned = 0;
            for (int slot = 0; slot < QuickSlotCount; slot++)
            {
                if (_propCarrier.TryGetSlot(slot, out ThrowableKind held)
                    && held == kind)
                {
                    owned += _propCarrier.GetSlotQuantity(slot);
                }
            }

            return owned;
        }

        /// <summary>
        /// Buys one, on the machine that decides.
        ///
        /// The rule is <see cref="PoliceSupplyCounter.TryInteract"/> and it is not
        /// duplicated here: it refuses for the wrong role, no money and no room, in
        /// that order, and hands the prop over before charging so a failure cannot
        /// take the money for nothing. This only asks.
        /// </summary>
        private void Buy(ThrowableKind kind, int price)
        {
            if (_propCarrier == null)
            {
                return;
            }

            if (!_propCarrier.CanStore(kind))
            {
                ShowNotice("퀵슬롯에 자리가 없어요.");
                return;
            }

            if (_policeWallet != null && !_policeWallet.CanAfford(price))
            {
                ShowNotice("골드가 부족해요.");
                return;
            }

            if (_propCarrier.IsRemoteControlled)
            {
                GameplayInputRouter.RequestPropPurchase((int)kind);
                return;
            }

            PlayerRoleIdentity identity =
                _propCarrier.GetComponent<PlayerRoleIdentity>();
            if (identity == null)
            {
                return;
            }

            foreach (PoliceSupplyCounter counter in
                FindObjectsByType<PoliceSupplyCounter>(FindObjectsSortMode.None))
            {
                if (counter != null
                    && counter.Kind == kind
                    && counter.TryInteract(
                        new PlayerInteractionContext(identity)))
                {
                    return;
                }
            }

            // No counter for this kind on this machine. Said out loud rather than
            // swallowed: the stall listed it, so silence would be the stall lying.
            ShowNotice("지금은 살 수 없어요.");
        }

        private void TickNotice()
        {
            if (_gridHint == null)
            {
                return;
            }

            if (_noticeSeconds > 0f)
            {
                _noticeSeconds -= Time.unscaledDeltaTime;
            }
            else if (!string.Equals(_gridHint.text, _gridHintText))
            {
                _gridHint.text = _gridHintText;
            }
        }

        /// <summary>
        /// Drops basket rows the bag can no longer honour.
        ///
        /// Without this the ledger keeps offering a gemstone that has since been
        /// sold, confiscated or handed to a hiding spot, and SELL would quietly
        /// sell fewer than the total said.
        /// </summary>
        private void PruneBasket(LootBag bag)
        {
            for (int index = _basketKinds.Count - 1; index >= 0; index--)
            {
                LootDefinition definition = _basketKinds[index];
                int held = CountInBag(bag, definition);
                if (held <= 0)
                {
                    _basketKinds.RemoveAt(index);
                    _basket.Remove(definition);
                    continue;
                }

                if (_basket[definition] > held)
                {
                    _basket[definition] = held;
                }
            }
        }

        private static int CountInBag(LootBag bag, LootDefinition definition)
        {
            if (bag == null || definition == null)
            {
                return 0;
            }

            int held = 0;
            for (int cell = 0; cell < bag.CellCount; cell++)
            {
                if (bag.TryGetCell(cell, out LootDefinition kind, out int count)
                    && kind == definition)
                {
                    held += count;
                }
            }

            return held;
        }

        private int BasketCount(LootDefinition definition)
        {
            return definition != null
                && _basket.TryGetValue(definition, out int count)
                ? count
                : 0;
        }

        /// <summary>
        /// Draws one of the four prop cells, greyed out.
        ///
        /// Shown but unsellable, and both halves matter. Hiding them would leave
        /// four empty cells under numbers 1-4 while the bag screen the player just
        /// closed had things in them — which reads as the merchant screen failing
        /// to load the bag rather than as the raccoon not buying rocks.
        /// </summary>
        private void BindPropCell(int index)
        {
            ThrowableKind kind = ThrowableKind.Rock;
            bool hasProp = _propCarrier != null
                && _propCarrier.TryGetSlot(index, out kind)
                && ThrowableCatalog.CanUseInQuickSlot(kind);
            Sprite icon = hasProp ? PropIcon(kind) : null;

            _cells[index]?.Bind(new InventorySlotViewModel(
                (index + 1).ToString(),
                icon,
                hasProp ? _propCarrier.GetSlotQuantity(index) : 0,
                false,
                // Disabled whether or not it holds something: a prop is never
                // sellable, and a cell that looks pickable but does nothing is
                // the refusal the player cannot see.
                true,
                hasProp && icon == null ? PropGlyph(kind) : string.Empty,
                string.Empty,
                0,
                null));
        }

        private static Sprite PropIcon(ThrowableKind kind)
        {
            string path = kind switch
            {
                ThrowableKind.Rock => "UI/ItemIcons/rock",
                ThrowableKind.Banana => "UI/ItemIcons/banana",
                ThrowableKind.GlueTrap => "UI/ItemIcons/can",
                ThrowableKind.SensorLight => "UI/ItemIcons/police lantern alarm",
                ThrowableKind.TunaCan => "UI/ItemIcons/fish can",
                ThrowableKind.DogTreat => "UI/ItemIcons/bone",
                ThrowableKind.RubberChicken => "UI/ItemIcons/yellow chicken",
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (IconCache.TryGetValue(path, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = Resources.Load<Sprite>(path);
            IconCache[path] = sprite;
            return sprite;
        }

        private static string PropGlyph(ThrowableKind kind)
        {
            return kind.ToString().Substring(0, 1);
        }

        /// <summary>
        /// Says why for a couple of seconds, in the line under the grid.
        /// </summary>
        private void ShowNotice(string message)
        {
            if (_gridHint == null)
            {
                return;
            }

            _gridHint.text = message;
            _noticeSeconds = 2.5f;
        }

        private void HandleCellClicked(int gridIndex)
        {
            int cell = gridIndex - QuickSlotCount;
            LootBag bag = _carrier != null ? _carrier.Cells : null;
            if (cell < 0)
            {
                ShowNotice("소품은 너구리가 사지 않아요.");
                return;
            }

            if (bag == null)
            {
                return;
            }

            if (!bag.TryGetCell(cell, out LootDefinition definition, out int count))
            {
                return;
            }

            if (BasketCount(definition) >= count)
            {
                return;
            }

            // The whole stack, not one. Five coins are one decision, and clicking
            // a cell five times to express it is five chances to miscount.
            if (!_basket.ContainsKey(definition))
            {
                if (_basketKinds.Count >= RowCount)
                {
                    return;
                }

                _basketKinds.Add(definition);
            }

            _basket[definition] = count;
            Refresh();
        }

        private void HandleBuyClicked(int row)
        {
            if (row < 0 || row >= _stock.Count)
            {
                return;
            }

            (ThrowableKind kind, int price) = _stock[row];
            Buy(kind, price);
        }

        private void HandleRemoveClicked(int row)
        {
            if (row < 0 || row >= _basketKinds.Count)
            {
                return;
            }

            LootDefinition definition = _basketKinds[row];
            _basketKinds.RemoveAt(row);
            _basket.Remove(definition);
            Refresh();
        }

        private void HandleSellClicked()
        {
            var kinds = new List<LootDefinition>(_basketKinds);
            foreach (LootDefinition definition in kinds)
            {
                Sell(definition, BasketCount(definition));
            }

            _basket.Clear();
            _basketKinds.Clear();
            Refresh();
        }

        private void HandleSellAllClicked()
        {
            LootBag bag = _carrier != null ? _carrier.Cells : null;
            if (bag == null)
            {
                return;
            }

            var kinds = new List<LootDefinition>();
            var counts = new List<int>();
            for (int cell = 0; cell < bag.CellCount; cell++)
            {
                if (!bag.TryGetCell(cell, out LootDefinition definition, out int count))
                {
                    continue;
                }

                int existing = kinds.IndexOf(definition);
                if (existing >= 0)
                {
                    counts[existing] += count;
                    continue;
                }

                kinds.Add(definition);
                counts.Add(count);
            }

            for (int index = 0; index < kinds.Count; index++)
            {
                Sell(kinds[index], counts[index]);
            }

            _basket.Clear();
            _basketKinds.Clear();
            Refresh();
        }

        /// <summary>
        /// Sells here when this machine owns the loot, and asks the host when it
        /// does not.
        ///
        /// Ownership is read off the pieces themselves:
        /// <see cref="LootItem.IsRemoteControlled"/> is already how a loot item
        /// says "another machine decides my state". Selling locally anyway would
        /// take the gemstone off this screen and put gold on it, and the host's
        /// next update would put both back — which reads as the SELL button
        /// half-working.
        /// </summary>
        private void Sell(LootDefinition definition, int count)
        {
            if (definition == null || count <= 0 || _carrier == null)
            {
                return;
            }

            if (IsAuthoritativeOver(definition))
            {
                _carrier.SellAllOfKind(
                    definition,
                    _wallet,
                    ResolveLootConfig(),
                    count);
                return;
            }

            GameplayInputRouter.RequestLootSale(definition.IdHash, count);
        }

        private bool IsAuthoritativeOver(LootDefinition definition)
        {
            foreach (LootItem item in _carrier.CarriedLoot)
            {
                if (item != null
                    && item.Definition == definition
                    && item.IsRemoteControlled)
                {
                    return false;
                }
            }

            return true;
        }

        private LootConfig ResolveLootConfig()
        {
            if (_lootConfig == null && GameConfigService.IsInitialized)
            {
                _lootConfig = GameConfigService.Current.Loot;
            }

            return _lootConfig;
        }

        private int UnitPrice(LootDefinition definition)
        {
            if (definition == null)
            {
                return 0;
            }

            LootConfig config = ResolveLootConfig();
            if (config != null)
            {
                return definition.GetPrice(config);
            }

            // A mirror of LootConfig's defaults, reached only when the config is
            // not up. It has to move whenever those move: a shop window is where
            // the player reads what a thing is worth, and a stale copy here
            // quotes one price while the purse credits another. It sat at
            // 200/350/500 through the cut to a fifth before anyone looked.
            return definition.Rarity switch
            {
                LootRarity.Uncommon => 70,
                LootRarity.Rare => 100,
                _ => 40
            };
        }

        /// <summary>
        /// Icons by stable id, looked up once.
        ///
        /// Cached because the ledger redraws every frame it is open and there are
        /// twenty-five cells: without this the screen does fifty string-keyed
        /// resource lookups a frame, on a build whose first target is WebGL.
        /// </summary>
        private static readonly Dictionary<string, Sprite> IconCache = new();

        private static Sprite CoinSprite =>
            _coin ??= Resources.Load<Sprite>("UI/CurrencyCoin");

        private static Sprite _coin;

        private static Sprite ResolveIcon(LootDefinition definition)
        {
            string stableId = definition != null ? definition.StableId : string.Empty;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                return null;
            }

            if (IconCache.TryGetValue(stableId, out Sprite cached))
            {
                return cached;
            }

            string path = stableId switch
            {
                "common-trinket" => "UI/ItemIcons/gold medal",
                "uncommon-watch" => "UI/ItemIcons/golden watch",
                "rare-jewel" => "UI/ItemIcons/blue gemstone",
                "jewel-sapphire" => "UI/ItemIcons/blue gemstone",
                "jewel-watch" => "UI/ItemIcons/golden watch",
                "jewel-gold-bar" => "UI/ItemIcons/gold medal",
                _ => string.Empty
            };
            Sprite sprite = string.IsNullOrWhiteSpace(path)
                ? null
                : Resources.Load<Sprite>(path);

            // Then the one baked from the piece's own model. The ledger and the
            // bag have to agree about what a thing looks like, or the player picks
            // a cell here that showed a different picture there.
            sprite ??= Resources.Load<Sprite>($"UI/ItemIcons/Loot/{stableId}");
            IconCache[stableId] = sprite;
            return sprite;
        }

        private static string GlyphFor(LootDefinition definition)
        {
            string name = definition != null ? definition.DisplayName : string.Empty;
            return string.IsNullOrWhiteSpace(name)
                ? "?"
                : name.Substring(0, 1);
        }

        private void EnsureBuilt()
        {
            // Rebuilt on a mode change rather than toggled. A machine only ever
            // has one role, so this happens once in practice — and building both
            // layouts and hiding one would leave a set of buttons wired to the
            // wrong half, which is the kind of thing that works until somebody
            // rematches into the other role.
            if (_window != null && _built && _builtMode != _mode)
            {
                Destroy(_window);
                _window = null;
                _rows.Clear();
                _cells = System.Array.Empty<InventorySlotView>();
                _built = false;
                _gridHint = null;
                _totalLabel = null;
                _emptyHint = null;
                _badge = null;
            }

            if (_window != null)
            {
                return;
            }

            _built = true;
            _builtMode = _mode;

            _window = HudRuntimeInstaller.CreatePanel(
                transform,
                "Merchant Trade",
                new Vector2(1400f, 660f));
            HudRuntimeInstaller.SkinWindow(_window);
            RectTransform windowRect = _window.GetComponent<RectTransform>();
            HudRuntimeInstaller.Anchor(
                windowRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f),
                new Vector2(1400f, 660f));

            BuildHeader();
            BuildInventorySide();
            BuildLedgerSide();
            _window.SetActive(false);
        }

        private void BuildHeader()
        {
            GameObject portrait = HudRuntimeInstaller.CreatePanel(
                _window.transform,
                "Merchant Portrait",
                new Vector2(96f, 96f));
            HudRuntimeInstaller.Skin(
                portrait,
                HudPanelSkin.Shape.Section,
                HudSpriteLibrary.SectionFill,
                HudSpriteLibrary.Accent,
                12,
                2);
            RectTransform portraitRect = portrait.GetComponent<RectTransform>();
            portraitRect.pivot = new Vector2(0f, 1f);
            HudRuntimeInstaller.Anchor(
                portraitRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(20f, -18f),
                new Vector2(96f, 96f));
            TMP_Text portraitLabel = HudRuntimeInstaller.CreateText(
                portrait.transform,
                "Label",
                "너구리",
                20f,
                TextAlignmentOptions.Center);
            portraitLabel.color = HudSpriteLibrary.Accent;
            HudRuntimeInstaller.Stretch(portraitLabel.rectTransform);

            // Pivoted top-left, not centred.
            //
            // `Anchor` places the **pivot**, and a fresh RectTransform pivots at
            // its middle. A 560-wide caption whose centre sits 132px from the
            // window's left edge therefore starts at -148 — outside the window,
            // over the map, on top of the portrait. It looked like a layout
            // opinion rather than a mistake, which is why it survived a
            // screenshot: every element was the right size and the wrong place by
            // exactly half its own width.
            TMP_Text title = HudRuntimeInstaller.CreateText(
                _window.transform,
                "Title",
                "RACCOON MARKET",
                34f,
                TextAlignmentOptions.Left);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            HudRuntimeInstaller.Anchor(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(136f, -24f),
                new Vector2(620f, 50f));

            TMP_Text subtitle = HudRuntimeInstaller.CreateText(
                _window.transform,
                "Subtitle",
                _mode == Mode.Buy
                    ? "너구리에게서 장비를 삽니다."
                    : "가방의 보물을 너구리에게 팝니다.",
                17f,
                TextAlignmentOptions.Left);
            subtitle.color = new Color(0.72f, 0.86f, 0.94f, 0.86f);
            subtitle.rectTransform.pivot = new Vector2(0f, 1f);
            HudRuntimeInstaller.Anchor(
                subtitle.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(136f, -78f),
                new Vector2(620f, 28f));

            _badge = HudRuntimeInstaller.BuildCurrencyBadge(
                _window.transform,
                "Merchant Currency",
                CurrencyBadgeView.Placement.Merchant,
                new Vector2(1f, 1f),
                new Vector2(-22f, -26f),
                new Vector2(178f, 50f));
        }

        private void BuildInventorySide()
        {
            GameObject section = HudRuntimeInstaller.CreatePanel(
                _window.transform,
                "Your Inventory",
                new Vector2(660f, 470f));
            HudRuntimeInstaller.Skin(
                section,
                HudPanelSkin.Shape.Section,
                HudSpriteLibrary.SectionFill,
                HudSpriteLibrary.BorderSoft,
                12,
                2);
            RectTransform rect = section.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0f, 1f);
            HudRuntimeInstaller.Anchor(
                rect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -136f),
                new Vector2(660f, 470f));

            TMP_Text title = HudRuntimeInstaller.CreateText(
                section.transform,
                "Title",
                _mode == Mode.Buy ? "내 장비" : "YOUR INVENTORY",
                20f,
                TextAlignmentOptions.Left);
            title.color = HudSpriteLibrary.Accent;
            HudRuntimeInstaller.Anchor(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(22f, -20f),
                new Vector2(-44f, 32f));

            var gridObject = new GameObject("Grid", typeof(RectTransform));
            gridObject.transform.SetParent(section.transform, false);
            HudRuntimeInstaller.Stretch(gridObject.GetComponent<RectTransform>());
            // 5 x 62 + 4 x 8 = 342, plus 62 above and 44 below = 448 inside a 470
            // section. Worked out rather than eyeballed: at 70px tall the fifth
            // row needed 496 and fell out of the bottom of the window, and a grid
            // that overflows draws its last row over the buttons rather than
            // complaining.
            var layout = gridObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(94f, 62f);
            layout.spacing = new Vector2(12f, 8f);
            layout.padding = new RectOffset(38, 38, 62, 44);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = GridColumns;

            _cells = new InventorySlotView[CellCount];
            for (int index = 0; index < CellCount; index++)
            {
                InventorySlotView cell = HudRuntimeInstaller.BuildInventorySlot(
                    gridObject.transform,
                    $"Merchant Cell {index + 1}",
                    new Vector2(94f, 62f),
                    (index + 1).ToString(),
                    18f);
                cell.SetCellIndex(index);
                _cells[index] = cell;

                // Bound here rather than by the builder. A listener added while
                // writing a prefab is non-persistent and would be gone from the
                // build (ISSUE-017); this window is assembled at runtime, so the
                // listener it adds is the one that runs.
                Button button = cell.GetComponent<Button>();
                if (button != null)
                {
                    int captured = index;
                    button.onClick.AddListener(() => HandleCellClicked(captured));
                }
            }

            TMP_Text hint = HudRuntimeInstaller.CreateText(
                section.transform,
                "Hint",
                _mode == Mode.Buy
                    ? "숫자키 1~4로 꺼내 씁니다."
                    : "칸을 눌러 판매 목록에 담으세요.",
                15f,
                TextAlignmentOptions.Left);
            hint.color = new Color(0.66f, 0.80f, 0.90f, 0.78f);
            _gridHint = hint;
            _gridHintText = hint.text;
            HudRuntimeInstaller.Anchor(
                hint.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(22f, 16f),
                new Vector2(-44f, 24f));
        }

        private void BuildLedgerSide()
        {
            GameObject section = HudRuntimeInstaller.CreatePanel(
                _window.transform,
                "Items To Sell",
                new Vector2(660f, 470f));
            HudRuntimeInstaller.Skin(
                section,
                HudPanelSkin.Shape.Section,
                HudSpriteLibrary.SectionFill,
                HudSpriteLibrary.BorderSoft,
                12,
                2);
            RectTransform rect = section.GetComponent<RectTransform>();
            rect.pivot = new Vector2(1f, 1f);
            HudRuntimeInstaller.Anchor(
                rect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -136f),
                new Vector2(660f, 470f));

            TMP_Text title = HudRuntimeInstaller.CreateText(
                section.transform,
                "Title",
                _mode == Mode.Buy ? "RACCOON GOODS" : "ITEMS TO SELL",
                20f,
                TextAlignmentOptions.Left);
            title.color = HudSpriteLibrary.Accent;
            HudRuntimeInstaller.Anchor(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(22f, -20f),
                new Vector2(-44f, 32f));

            for (int index = 0; index < RowCount; index++)
            {
                _rows.Add(BuildRow(section.transform, index));
            }

            _emptyHint = HudRuntimeInstaller.CreateText(
                section.transform,
                "Empty Hint",
                "왼쪽에서 팔 보물을 고르세요.",
                17f,
                TextAlignmentOptions.Center);
            _emptyHint.color = new Color(0.62f, 0.76f, 0.86f, 0.72f);
            HudRuntimeInstaller.Anchor(
                _emptyHint.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -140f),
                new Vector2(-60f, 30f));

            BuildTotalRow(section.transform);
            BuildButtons(section.transform);

            TMP_Text note = HudRuntimeInstaller.CreateText(
                section.transform,
                "Note",
                _mode == Mode.Buy
                    ? "사는 화면입니다. 파는 곳이 아닙니다."
                    : "파는 화면입니다. 사는 곳이 아닙니다.",
                15f,
                TextAlignmentOptions.Left);
            note.color = new Color(0.66f, 0.80f, 0.90f, 0.78f);
            HudRuntimeInstaller.Anchor(
                note.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(22f, 16f),
                new Vector2(-44f, 24f));
        }

        private LedgerRow BuildRow(Transform parent, int index)
        {
            GameObject row = HudRuntimeInstaller.CreatePanel(
                parent,
                $"Sell Row {index + 1}",
                new Vector2(616f, 52f));
            HudRuntimeInstaller.Skin(
                row,
                HudPanelSkin.Shape.Slot,
                new Color(0.031f, 0.075f, 0.114f, 0.95f),
                HudSpriteLibrary.BorderSoft,
                8,
                2);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.pivot = new Vector2(0.5f, 1f);
            HudRuntimeInstaller.Anchor(
                rowRect,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -(62f + (index * 58f))),
                new Vector2(-44f, 52f));

            Image icon = HudRuntimeInstaller.CreateImage(row.transform, "Item Icon", Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            HudRuntimeInstaller.Anchor(
                icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(34f, 0f),
                new Vector2(40f, 40f));

            // Both pivoted to their left edge, and both inside the row.
            //
            // `Anchor` places the **pivot**, and a fresh RectTransform pivots at
            // its middle — so a 260-wide caption whose centre sat 66px from the
            // row's left edge actually started at -64, outside the row, printed
            // over the section behind it. The name and the stock count were
            // hanging off the left of every shelf line and reading as labels for
            // the panel rather than for the goods.
            TMP_Text name = HudRuntimeInstaller.CreateText(
                row.transform,
                "Name",
                string.Empty,
                19f,
                TextAlignmentOptions.Left);
            name.rectTransform.pivot = new Vector2(0f, 0.5f);
            HudRuntimeInstaller.Anchor(
                name.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(66f, 0f),
                new Vector2(220f, 30f));

            // Beside the name rather than under it, and short. "보유 2" is the one
            // thing the officer checks before spending, and stacked under the name
            // it read as a second line of the name.
            TMP_Text count = HudRuntimeInstaller.CreateText(
                row.transform,
                "Count",
                string.Empty,
                16f,
                TextAlignmentOptions.Left);
            count.color = new Color(0.72f, 0.86f, 0.94f, 0.88f);
            count.rectTransform.pivot = new Vector2(0f, 0.5f);
            HudRuntimeInstaller.Anchor(
                count.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(292f, 0f),
                new Vector2(80f, 26f));

            // Laid out from the right edge inwards, measured off the button this
            // row ends with rather than off a hand-tuned constant. Two rounds of
            // eyeballing put the figure underneath the officer's 86px BUY button,
            // where it was drawn and invisible — the shop listed goods with a coin
            // and no price at all, which reads as the goods being free.
            //
            // Coin first, then the number, both left of the button and neither
            // touching it. Left-aligned rather than right so the rows line up on
            // the coin instead of on the last digit.
            bool buying = _mode == Mode.Buy;
            float actionWidth = buying ? 86f : 46f;
            const float PriceWidth = 92f;
            const float CoinSize = 26f;

            // The button's own inset from the right edge, so this stays correct if
            // the button moves.
            float actionSpan = actionWidth + 12f;
            float priceLeft = -(actionSpan + 14f + PriceWidth);
            float coinRight = priceLeft - 8f;

            Image coin = HudRuntimeInstaller.CreateImage(row.transform, "Coin", Color.white);
            coin.sprite = CoinSprite;
            coin.preserveAspect = true;
            coin.raycastTarget = false;
            coin.enabled = coin.sprite != null;
            coin.rectTransform.pivot = new Vector2(1f, 0.5f);
            HudRuntimeInstaller.Anchor(
                coin.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(coinRight, 0f),
                new Vector2(CoinSize, CoinSize));

            TMP_Text price = HudRuntimeInstaller.CreateText(
                row.transform,
                "Price",
                string.Empty,
                20f,
                TextAlignmentOptions.Left);
            price.color = HudSpriteLibrary.Gold;

            // Stated rather than left at the default, and 36px for a 20pt line.
            // TMP draws *nothing at all* when the rect is shorter than one line
            // and the mode is Ellipsis (ISSUE-047), and a price that silently
            // disappears is indistinguishable from a price nobody set.
            price.overflowMode = TextOverflowModes.Overflow;
            price.enableWordWrapping = false;
            price.rectTransform.pivot = new Vector2(0f, 0.5f);
            HudRuntimeInstaller.Anchor(
                price.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(priceLeft, 0f),
                new Vector2(PriceWidth, 36f));

            GameObject actionObject = HudRuntimeInstaller.CreatePanel(
                row.transform,
                buying ? "Buy" : "Remove",
                new Vector2(actionWidth, 38f));
            HudRuntimeInstaller.Skin(
                actionObject,
                HudPanelSkin.Shape.Solid,
                buying
                    ? HudSpriteLibrary.SellGreen
                    : new Color(0.20f, 0.06f, 0.08f, 0.95f),
                Color.clear,
                8,
                0);
            RectTransform actionRect = actionObject.GetComponent<RectTransform>();
            actionRect.pivot = new Vector2(1f, 0.5f);
            HudRuntimeInstaller.Anchor(
                actionRect,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-12f, 0f),
                new Vector2(actionWidth, 38f));
            TMP_Text actionLabel = HudRuntimeInstaller.CreateText(
                actionObject.transform,
                "Label",
                buying ? "BUY" : "X",
                buying ? 17f : 19f,
                TextAlignmentOptions.Center);
            actionLabel.color = buying
                ? Color.white
                : new Color(1f, 0.72f, 0.72f, 1f);
            HudRuntimeInstaller.Stretch(actionLabel.rectTransform);
            var actionButton = actionObject.AddComponent<Button>();
            actionButton.targetGraphic = actionObject.GetComponent<Image>();
            int captured = index;
            if (buying)
            {
                actionButton.onClick.AddListener(() => HandleBuyClicked(captured));
            }
            else
            {
                actionButton.onClick.AddListener(() => HandleRemoveClicked(captured));
            }

            row.SetActive(false);
            return new LedgerRow
            {
                Root = row,
                Icon = icon,
                Name = name,
                Count = count,
                Price = price,
                Action = actionButton,
                ActionLabel = actionLabel
            };
        }

        private void BuildTotalRow(Transform parent)
        {
            GameObject total = HudRuntimeInstaller.CreatePanel(
                parent,
                "Total",
                new Vector2(616f, 54f));
            HudRuntimeInstaller.Skin(
                total,
                HudPanelSkin.Shape.Solid,
                new Color(0.02f, 0.05f, 0.08f, 0.95f),
                Color.clear,
                10,
                0);
            RectTransform rect = total.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0f);
            HudRuntimeInstaller.Anchor(
                rect,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 118f),
                new Vector2(-44f, 54f));

            TMP_Text label = HudRuntimeInstaller.CreateText(
                total.transform,
                "Label",
                _mode == Mode.Buy ? "보유 골드" : "TOTAL",
                21f,
                TextAlignmentOptions.Left);
            label.color = HudSpriteLibrary.Accent;
            HudRuntimeInstaller.Anchor(
                label.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(96f, 0f),
                new Vector2(160f, 32f));

            // Both pivoted to their right edge.
            //
            // `Anchor` places the pivot, and a fresh RectTransform pivots at its
            // centre — so a 120-wide figure centred 24px inside the right edge
            // actually reached 36px **past** it, and the right-aligned text sat out
            // there over the map. Same mistake as the window title: every size
            // right, every position off by half its own width.
            Image coin = HudRuntimeInstaller.CreateImage(total.transform, "Coin", Color.white);
            coin.sprite = CoinSprite;
            coin.preserveAspect = true;
            coin.raycastTarget = false;
            coin.enabled = coin.sprite != null;
            coin.rectTransform.pivot = new Vector2(1f, 0.5f);
            HudRuntimeInstaller.Anchor(
                coin.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-176f, 0f),
                new Vector2(28f, 28f));

            _totalLabel = HudRuntimeInstaller.CreateText(
                total.transform,
                "Amount",
                "0",
                27f,
                TextAlignmentOptions.Right);
            _totalLabel.color = HudSpriteLibrary.Gold;
            _totalLabel.rectTransform.pivot = new Vector2(1f, 0.5f);
            HudRuntimeInstaller.Anchor(
                _totalLabel.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-28f, 0f),
                new Vector2(140f, 40f));
        }

        private void BuildButtons(Transform parent)
        {
            if (_mode == Mode.Buy)
            {
                // One button. Buying happens on the row, next to the price, where
                // the decision is — a shop-wide "BUY" would have to mean "buy
                // what?", and SELL ALL has no meaning on this side of the counter.
                _cancelButton = BuildButton(
                    parent,
                    "Close",
                    "닫기",
                    HudSpriteLibrary.CancelRed,
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 40f),
                    Close);
                return;
            }

            _sellButton = BuildButton(
                parent,
                "Sell",
                "SELL",
                HudSpriteLibrary.HeaderTeal,
                new Vector2(0f, 0f),
                new Vector2(24f, 40f),
                HandleSellClicked);
            _sellAllButton = BuildButton(
                parent,
                "Sell All",
                "SELL ALL",
                HudSpriteLibrary.SellGreen,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 40f),
                HandleSellAllClicked);
            _cancelButton = BuildButton(
                parent,
                "Cancel",
                "CANCEL",
                HudSpriteLibrary.CancelRed,
                new Vector2(1f, 0f),
                new Vector2(-24f, 40f),
                Close);
        }

        private static Button BuildButton(
            Transform parent,
            string name,
            string caption,
            Color fill,
            Vector2 anchor,
            Vector2 position,
            UnityEngine.Events.UnityAction action)
        {
            GameObject button = HudRuntimeInstaller.CreatePanel(
                parent,
                name,
                new Vector2(190f, 56f));
            HudRuntimeInstaller.Skin(
                button,
                HudPanelSkin.Shape.Solid,
                fill,
                Color.clear,
                10,
                0);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.pivot = new Vector2(anchor.x, 0f);
            HudRuntimeInstaller.Anchor(
                rect,
                anchor,
                anchor,
                position,
                new Vector2(190f, 56f));

            TMP_Text label = HudRuntimeInstaller.CreateText(
                button.transform,
                "Label",
                caption,
                21f,
                TextAlignmentOptions.Center);

            // 40px of height for a 21pt caption. TMP draws nothing at all when the
            // rect is shorter than one line, so a button whose label vanishes looks
            // like a button that does nothing (ISSUE-047).
            HudRuntimeInstaller.Anchor(
                label.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                new Vector2(-16f, 40f));

            var component = button.AddComponent<Button>();
            component.targetGraphic = button.GetComponent<Image>();
            component.onClick.AddListener(action);
            return component;
        }
    }
}
