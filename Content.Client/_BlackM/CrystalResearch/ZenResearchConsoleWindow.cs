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
    private readonly Label _balance = new() { ClipText = true };
    private readonly Label _status = new() { ClipText = true };
    private readonly RichTextLabel _deliveries = new();
    private readonly BoxContainer _perks = Column();
    private readonly BoxContainer _supplies = Column();
    private readonly Dictionary<string, Button> _perkButtons = new();
    private readonly Dictionary<string, Button> _supplyButtons = new();
    private string _catalog = string.Empty;

    public ZenResearchConsoleWindow()
    {
        Title = Loc.GetString("zen-research-ui-title");
        MinSize = new Vector2(510, 380);
        SetSize = new Vector2(640, 540);
        var root = Column();
        root.AddChild(_balance);
        root.AddChild(_status);
        var tabs = new TabContainer { VerticalExpand = true, HorizontalExpand = true };
        var perkScroll = new ScrollContainer();
        perkScroll.AddChild(_perks);
        tabs.AddChild(perkScroll);
        tabs.SetTabTitle(0, Loc.GetString("zen-research-ui-perks"));
        var supplyScroll = new ScrollContainer();
        supplyScroll.AddChild(_supplies);
        tabs.AddChild(supplyScroll);
        tabs.SetTabTitle(1, Loc.GetString("zen-research-ui-supplies"));
        root.AddChild(tabs);
        root.AddChild(_deliveries);
        Contents.AddChild(root);
    }

    private static BoxContainer Column() => new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        HorizontalExpand = true,
        SeparationOverride = 10,
    };

    public void UpdateState(ZenResearchConsoleState state)
    {
        _balance.Text = Loc.GetString("zen-research-ui-balance", ("breakthroughs", state.Breakthroughs), ("points", state.Points));
        _status.Text = !state.Powered ? Loc.GetString("zen-crystal-ui-no-power") :
            !state.Connected ? Loc.GetString("zen-crystal-no-server") : Loc.GetString("zen-research-ui-connected");
        var catalog = string.Join("|", state.Perks.Select(p => $"{p.Id}:{p.Level}:{p.Cost}")) + ";" +
                      string.Join("|", state.Supplies.Select(s => $"{s.Id}:{s.Stock}:{s.Cost}"));
        if (catalog != _catalog)
        {
            _catalog = catalog;
            RebuildCatalog(state);
        }
        foreach (var perk in state.Perks)
        {
            if (_perkButtons.TryGetValue(perk.Id, out var button))
                button.Disabled = !state.Powered || !state.Connected || perk.Level >= perk.Maximum || state.Breakthroughs < perk.Cost;
        }
        foreach (var supply in state.Supplies)
        {
            if (_supplyButtons.TryGetValue(supply.Id, out var button))
                button.Disabled = !state.Powered || !state.Connected || supply.Stock <= 0 || state.Points < supply.Cost || state.Deliveries.Count >= 5;
        }
        _deliveries.SetMessage(string.Join("\n", state.Deliveries));
    }

    private void RebuildCatalog(ZenResearchConsoleState state)
    {
        _perks.RemoveAllChildren();
        _supplies.RemoveAllChildren();
        _perkButtons.Clear();
        _supplyButtons.Clear();
        foreach (var perk in state.Perks)
        {
            var item = Column();
            item.AddChild(new Label { Text = $"{perk.Name} / {perk.Level}/{perk.Maximum}", ClipText = true });
            var description = new RichTextLabel();
            description.SetMessage(perk.Description);
            item.AddChild(description);
            var button = new Button { Text = Loc.GetString("zen-research-ui-unlock", ("cost", perk.Cost)), HorizontalExpand = true };
            button.OnPressed += _ => Purchase?.Invoke(perk.Id, true);
            item.AddChild(button);
            _perks.AddChild(item);
            _perkButtons[perk.Id] = button;
        }
        foreach (var supply in state.Supplies)
        {
            var row = new BoxContainer { SeparationOverride = 8 };
            var preview = new EntityPrototypeView { MinSize = new Vector2(40, 40), SetSize = new Vector2(40, 40) };
            preview.SetPrototype(supply.Product);
            row.AddChild(preview);
            var info = Column();
            info.AddChild(new Label { Text = supply.Name, ClipText = true });
            info.AddChild(new Label { Text = Loc.GetString("zen-research-ui-stock", ("stock", supply.Stock), ("seconds", supply.Seconds)), ClipText = true });
            row.AddChild(info);
            var button = new Button { Text = Loc.GetString("zen-research-ui-order", ("cost", supply.Cost)), MinWidth = 110 };
            button.OnPressed += _ => Purchase?.Invoke(supply.Id, false);
            row.AddChild(button);
            _supplies.AddChild(row);
            _supplyButtons[supply.Id] = button;
        }
    }
}
