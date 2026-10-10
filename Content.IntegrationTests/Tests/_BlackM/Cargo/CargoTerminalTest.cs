using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client._BlackM.Cargo.UI;
using Content.Shared.Cargo;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._BlackM.Cargo;

[TestFixture]
public sealed class CargoTerminalTest
{
    [Test]
    public async Task CatalogOrdersAndLayout()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        CargoTerminalWindow window = null!;
        EntityUid owner = default;
        EntityUid station = default;
        CargoConsoleInterfaceState state = null!;
        var prototypes = client.ResolveDependency<IPrototypeManager>();

        await client.WaitAssertion(() =>
        {
            owner = client.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            client.EntMan.AddComponent<CargoOrderConsoleComponent>(owner);
            station = client.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            client.EntMan.AddComponent<StationBankAccountComponent>(station);
            var capacitor = prototypes.Index<CargoProductPrototype>("XenExperimentCapacitorMk3Product");
            var sample = prototypes.Index<CargoProductPrototype>("XenRedSampleProduct");
            state = new CargoConsoleInterfaceState("Black Mesa", 1, 20, client.EntMan.GetNetEntity(station),
            [
                new CargoOrderData(1, capacitor.Product, capacitor.Name, 2500, 2, "Researcher", "Test", "Cargo", 0),
                new CargoOrderData(2, sample.Product, sample.Name, 1000, 1, "Scientist", "Test", "Science", 0),
                new CargoOrderData(3, sample.Product, sample.Name, 1000, 1, "Researcher", "Test", "Cargo", 0) { Approved = true },
            ],
            ["XenExperimentCapacitorMk3Product", "XenRedSampleProduct", "AmsPartCoreProduct"]);
            window = new CargoTerminalWindow();
            window.Initialize(owner, "Researcher");
            window.UpdateState(state);
            window.OpenCentered();

            Assert.That(window.FindControl<BoxContainer>("ProductList").ChildCount, Is.EqualTo(3));
            Assert.That(window.FindControl<BoxContainer>("OrderList").ChildCount, Is.EqualTo(2));
            Assert.That(window.FindControl<Label>("PendingTotalLabel").Text, Is.EqualTo(5000.ToString("N0", CultureInfo.CurrentCulture)));
            Assert.That(window.FindControl<Label>("RemainingLabel").Text, Is.EqualTo((-3000).ToString("N0", CultureInfo.CurrentCulture)));
            Assert.That(window.FindControl<Button>("SubmitButton").Disabled, Is.True);
        });

        async Task Click(BaseButton button)
        {
            var coords = new ScreenCoordinates(button.GlobalPixelPosition + button.PixelSize / 2, button.Window?.Id ?? default);
            foreach (var keyState in new[] { BoundKeyState.Down, BoundKeyState.Up })
            {
                var args = new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, keyState, coords, default, button.Size / 2, button.PixelSize / 2);
                await client.DoGuiEvent(button, args);
            }
        }

        var firstProduct = window.FindControl<BoxContainer>("ProductList").Children.OfType<BaseButton>().First();
        await Click(firstProduct);
        await client.WaitAssertion(() =>
        {
            Assert.That(window.FindControl<Button>("SubmitButton").Disabled, Is.False);
            window.FindControl<LineEdit>("Reason").Text = "Keep draft";
            window.FindControl<SpinBox>("Amount").Value = 2;
            window.UpdateState(state);
            Assert.That(window.FindControl<LineEdit>("Reason").Text, Is.EqualTo("Keep draft"));
            Assert.That(window.FindControl<SpinBox>("Amount").Value, Is.EqualTo(2));
            Assert.That(window.FindControl<Button>("SubmitButton").Disabled, Is.False);

            foreach (var size in new[] { new Vector2(1080, 650), new Vector2(800, 560), new Vector2(640, 420) })
            {
                window.SetSize = size;
                window.FindControl<BoxContainer>("CategoryColumn").Visible = size.X >= 900;
                window.FindControl<OptionButton>("CompactCategories").Visible = size.X < 900;
                window.Measure(size);
                window.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                Assert.That(window.DesiredSize.X, Is.LessThanOrEqualTo(size.X + 1), $"Width at {size}");
                Assert.That(window.DesiredSize.Y, Is.LessThanOrEqualTo(size.Y + 1), $"Height at {size}");
                Assert.That(window.FindControl<ScrollContainer>("ProductScroll").Height, Is.GreaterThanOrEqualTo(64), $"Catalog remains usable at {size}");
                Assert.That(window.FindControl<BoxContainer>("OrderColumn").Width, Is.GreaterThanOrEqualTo(170), $"Orders remain usable at {size}");
                foreach (var name in new[] { "SubmitButton", "RemainingLabel", "StatusLabel" })
                {
                    var control = window.FindControl<Control>(name);
                    Assert.That(control.GlobalPosition.Y + control.Height, Is.LessThanOrEqualTo(window.GlobalPosition.Y + size.Y + 1), name);
                }
            }
        });
        var submitted = false;
        window.OrderSubmitted += (_, amount, requester, reason) =>
        {
            Assert.That(amount, Is.EqualTo(2));
            Assert.That(requester, Is.EqualTo("Researcher"));
            Assert.That(reason, Is.EqualTo("Keep draft"));
            submitted = true;
        };
        await Click(window.FindControl<Button>("SubmitButton"));
        Assert.That(submitted, Is.True);
        await Click(window.FindControl<Button>("ApprovedTab"));
        await client.WaitAssertion(() => Assert.That(window.FindControl<BoxContainer>("OrderList").ChildCount, Is.EqualTo(1)));
        await client.WaitAssertion(() =>
        {
            window.Dispose();
            client.EntMan.DeleteEntity(owner);
            client.EntMan.DeleteEntity(station);
        });
        await pair.CleanReturnAsync();
    }
}
