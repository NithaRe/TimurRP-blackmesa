using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._BlackM.CrystalResearch;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._BlackM.CrystalResearch;

public sealed class ZenResearchConsoleWindow : DefaultWindow
{
    public event Action<string, bool>? Purchase;
    private readonly Label _breakthroughs = new() { FontColorOverride = ZenResearchUi.Amber, HorizontalExpand = true };
    private readonly Label _points = new() { FontColorOverride = ZenResearchUi.Green, HorizontalExpand = true };
    private readonly RichTextLabel _status = ZenResearchUi.Text();
    private readonly BoxContainer _orders = ZenResearchUi.Column(8);
    private readonly BoxContainer _catalog;
    private readonly ScrollContainer _orderScroll;
    private readonly BoxContainer _list = ZenResearchUi.Column(4);
    private readonly Dictionary<string, ContainerButton> _buttons = new();
    private readonly RichTextLabel _name = ZenResearchUi.Text();
    private readonly RichTextLabel _description = ZenResearchUi.Text();
    private readonly RichTextLabel _task = ZenResearchUi.Text();
    private readonly RichTextLabel _numbers = ZenResearchUi.Text();

    private readonly EntityPrototypeView _preview = new()
        { MinSize = new Vector2(64, 64), MaxSize = new Vector2(96, 96), Scale = new Vector2(2f) };

    private readonly Button _confirm = new() { HorizontalExpand = true, MinHeight = 32 };
    private enum View { Samples, Perks, Supplies, Orders }
    private readonly Dictionary<View, Button> _modes = new();
    private View _view;
    private string? _selectedSample;
    private string _shownJournal = "";
    private int _shownSample;
    private readonly OptionButton _category = new() { HorizontalExpand = true };
    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private ZenResearchConsoleState _state = new();
    private string? _selectedPerk;
    private string? _selectedSupply;
    private bool Perks => _view == View.Perks;
    private bool Samples => _view == View.Samples;
    private int _categoryId = -1;
    private string? _previewId;

    public ZenResearchConsoleWindow()
    {
        Title = Loc.GetString("zen-research-ui-title");
        MinSize = new Vector2(700, 460);
        SetSize = new Vector2(800, 560);
        var root = ZenResearchUi.Column(10);
        var balances = new BoxContainer { SeparationOverride = 16 };
        balances.AddChild(_breakthroughs);
        balances.AddChild(_points);
        root.AddChild(balances);
        root.AddChild(_status);
        root.AddChild(ZenResearchUi.Rule());
        var modes = new BoxContainer { SeparationOverride = 4 };
        foreach (var view in Enum.GetValues<View>())
        {
            var button = new Button
            {
                Text = Loc.GetString("zen-lab-tab-" + view.ToString().ToLowerInvariant()),
                ToggleMode = true, HorizontalExpand = true, Pressed = view == View.Samples,
            };
            button.OnPressed += _ => SelectMode(view);
            _modes.Add(view, button);
            modes.AddChild(button);
        }
        root.AddChild(modes);

        var body = new BoxContainer { HorizontalExpand = true, VerticalExpand = true, SeparationOverride = 16 };
        _catalog = body;
        var left = ZenResearchUi.Column(6);
        left.MinWidth = 200;
        left.MaxWidth = 220;
        _search.PlaceHolder = Loc.GetString("zen-lab-ui-search");
        _search.OnTextChanged += _ => RebuildList();
        _category.AddItem(Loc.GetString("zen-lab-category-all"), -1);
        foreach (var category in Enum.GetValues<ZenResearchSupplyCategory>())
            _category.AddItem(Loc.GetString("zen-lab-category-" + category.ToString().ToLowerInvariant()),
                (int) category);
        _category.SelectId(-1);
        _category.Visible = false;
        _category.OnItemSelected += args =>
        {
            _categoryId = args.Id;
            _category.SelectId(args.Id);
            RebuildList();
        };
        left.AddChild(_search);
        left.AddChild(_category);
        left.AddChild(ZenResearchUi.Scroll(_list));
        body.AddChild(left);

        var detail = ZenResearchUi.Column(12);
        detail.AddChild(_preview);
        detail.AddChild(_name);
        detail.AddChild(ZenResearchUi.Rule());
        detail.AddChild(_description);
        detail.AddChild(_numbers);
        var right = ZenResearchUi.Column(10);
        right.AddChild(_task);
        right.AddChild(ZenResearchUi.Scroll(detail));
        _confirm.OnPressed += _ =>
        {
            var id = Perks ? _selectedPerk : _selectedSupply;
            if (id != null && !_confirm.Disabled)
                Purchase?.Invoke(id, Perks);
        };
        right.AddChild(_confirm);
        body.AddChild(right);
        root.AddChild(body);
        _orderScroll = ZenResearchUi.Scroll(_orders);
        _orderScroll.Visible = false;
        root.AddChild(_orderScroll);
        Contents.AddChild(ZenResearchUi.Screen(root));
    }

    private void SelectMode(View view)
    {
        _view = view;
        foreach (var (mode, button) in _modes)
            button.Pressed = view == mode;
        _category.Visible = view == View.Supplies;
        _catalog.Visible = view != View.Orders;
        _orderScroll.Visible = view == View.Orders;
        _confirm.Visible = view is View.Perks or View.Supplies;
        RebuildList();
    }

    public void UpdateState(ZenResearchConsoleState state)
    {
        var changed = !_state.Perks.SequenceEqual(state.Perks) || !_state.Supplies.SequenceEqual(state.Supplies) ||
            !_state.Samples.Select(s => (s.Number, s.Name, s.Stage)).SequenceEqual(state.Samples.Select(s => (s.Number, s.Name, s.Stage)));
        var ordersChanged = !_state.Deliveries.SequenceEqual(state.Deliveries);
        _state = state;
        _breakthroughs.Text = Loc.GetString("zen-lab-ui-breakthroughs", ("amount", state.Breakthroughs));
        _points.Text = Loc.GetString("zen-lab-ui-points", ("amount", state.Points));
        _status.SetMessage(!state.Powered ? Loc.GetString("zen-crystal-ui-no-power") :
            !state.Connected ? Loc.GetString("zen-crystal-no-server") : Loc.GetString("zen-research-ui-connected"));
        if (ordersChanged || _orders.ChildCount == 0)
            UpdateOrders();
        if (changed || _buttons.Count == 0)
            RebuildList();
        else
            UpdateDetails();
    }

    private void UpdateOrders()
    {
        _orders.RemoveAllChildren();
        if (_state.Deliveries.Count == 0)
            _orders.AddChild(ZenResearchUi.Text(Loc.GetString("zen-lab-ui-no-deliveries")));
        foreach (var order in _state.Deliveries.OrderBy(d => d.Delivered).ThenByDescending(d => d.Number))
        {
            var row = ZenResearchUi.Column(4);
            row.AddChild(ZenResearchUi.Text(Loc.GetString("zen-lab-order-heading",
                ("number", order.Number), ("name", order.Name))));
            var status = ZenResearchUi.Text(order.Delivered
                ? Loc.GetString("zen-lab-order-delivered")
                : order.Seconds <= 0 ? Loc.GetString("zen-lab-order-waiting")
                : Loc.GetString("zen-lab-order-enroute", ("seconds", order.Seconds)));
            row.AddChild(status);
            row.AddChild(ZenResearchUi.Rule());
            _orders.AddChild(row);
        }
    }

    private void RebuildList()
    {
        _list.RemoveAllChildren();
        _buttons.Clear();
        var filter = _search.Text.Trim();
        if (Samples)
        {
            foreach (var sample in _state.Samples.Where(s => s.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))
                AddEntry(sample.Number.ToString(),
                    Loc.GetString("zen-lab-sample-heading", ("number", sample.Number), ("name", sample.Name)), sample.Stage);
        }
        else if (Perks)
        {
            foreach (var perk in _state.Perks.Where(p =>
                         p.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))
                AddEntry(perk.Id,
                    perk.Name,
                    Loc.GetString("zen-lab-ui-level", ("level", perk.Level), ("maximum", perk.Maximum)));
        }
        else
        {
            foreach (var supply in _state.Supplies.Where(s =>
                         (_categoryId == -1 || (int) s.Category == _categoryId) &&
                         s.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))
                AddEntry(supply.Id,
                    supply.Name,
                    Loc.GetString("zen-lab-ui-supply-summary", ("cost", supply.Cost), ("stock", supply.Stock)));
        }

        var selected = Samples ? _selectedSample : Perks ? _selectedPerk : _selectedSupply;
        if (selected == null || !_buttons.ContainsKey(selected))
        {
            selected = _buttons.Keys.FirstOrDefault();
            if (Samples)
                _selectedSample = selected;
            else if (Perks)
                _selectedPerk = selected;
            else
                _selectedSupply = selected;
        }

        foreach (var (id, button) in _buttons)
            button.Pressed = id == selected;
        if (_buttons.Count == 0)
            _list.AddChild(ZenResearchUi.Text(Loc.GetString("zen-lab-ui-empty-list")));
        UpdateDetails();
    }

    private void AddEntry(string id, string name, string caption)
    {
        var button = new ContainerButton { ToggleMode = true, HorizontalExpand = true, MinHeight = 56 };
        var text = ZenResearchUi.Column(4);
        text.AddChild(ZenResearchUi.Text(name));
        text.AddChild(new Label { Text = caption, FontColorOverride = ZenResearchUi.Muted, ClipText = true });
        button.AddChild(text);
        button.OnPressed += _ =>
        {
            if (Samples)
                _selectedSample = id;
            else if (Perks)
                _selectedPerk = id;
            else
                _selectedSupply = id;
            foreach (var (key, entry) in _buttons)
                entry.Pressed = key == id;
            UpdateDetails();
        };
        _buttons[id] = button;
        _list.AddChild(button);
    }

    private void UpdateDetails()
    {
        var ready = _state.Powered && _state.Connected;
        _preview.Visible = _view == View.Supplies;
        _confirm.Visible = _view is View.Perks or View.Supplies;
        _numbers.Visible = !Samples;
        _task.Visible = Samples;
        if (Samples && _state.Samples.FirstOrDefault(s => s.Number.ToString() == _selectedSample) is { } sample)
        {
            _task.SetMessage(Loc.GetString("zen-lab-task-heading") + "\n" + sample.Task);
            _name.SetMessage(Loc.GetString("zen-lab-sample-heading", ("number", sample.Number), ("name", sample.Name)) +
                "\n" + sample.Stage);
            if (_shownSample != sample.Number || _shownJournal != sample.Journal)
            {
                _shownSample = sample.Number;
                _shownJournal = sample.Journal;
                _description.SetMessage(sample.Journal);
            }
        }
        else if (Perks && _state.Perks.FirstOrDefault(p => p.Id == _selectedPerk) is { } perk)
        {
            _shownSample = 0;
            _name.SetMessage(perk.Name);
            _description.SetMessage(perk.Description);
            _numbers.SetMessage(Loc.GetString("zen-lab-ui-level", ("level", perk.Level), ("maximum", perk.Maximum)) +
                                "\n" + Loc.GetString("zen-lab-ui-perk-cost", ("cost", perk.Cost)));
            _confirm.Text =
                Loc.GetString(perk.Level >= perk.Maximum ? "zen-lab-ui-max-level" : "zen-research-ui-unlock",
                    ("cost", perk.Cost));
            _confirm.Disabled = !ready || perk.Level >= perk.Maximum || _state.Breakthroughs < perk.Cost;
        }
        else if (_view == View.Supplies && _state.Supplies.FirstOrDefault(s => s.Id == _selectedSupply) is { } supply)
        {
            if (_previewId != supply.Product)
            {
                _preview.SetPrototype(supply.Product);
                _previewId = supply.Product;
            }

            _shownSample = 0;
            _name.SetMessage(supply.Name);
            _description.SetMessage(Loc.GetString("zen-lab-category-" + supply.Category.ToString().ToLowerInvariant()));
            _numbers.SetMessage(Loc.GetString("zen-lab-ui-supply-detail",
                ("cost", supply.Cost),
                ("stock", supply.Stock),
                ("seconds", supply.Seconds)));
            _confirm.Text = Loc.GetString("zen-research-ui-order", ("cost", supply.Cost));
            _confirm.Disabled = !ready || supply.Stock <= 0 || _state.Points < supply.Cost ||
                                _state.Deliveries.Count(d => !d.Delivered) >= 5;
        }
        else
        {
            _shownSample = 0;
            _task.SetMessage(string.Empty);
            _name.SetMessage(string.Empty);
            _description.SetMessage(string.Empty);
            _numbers.SetMessage(string.Empty);
            _confirm.Disabled = true;
            _confirm.Text = Loc.GetString("zen-lab-ui-empty-list");
            _preview.Visible = false;
        }
    }
}
