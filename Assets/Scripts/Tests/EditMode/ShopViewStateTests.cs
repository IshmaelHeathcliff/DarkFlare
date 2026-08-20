using System.Collections.Generic;
using DarkFlare;
using NUnit.Framework;

public class ShopViewStateTests
{
    [Test]
    public void ResolveIndex_RestoresSameInstanceId()
    {
        List<string> items = new List<string> { "a", "b", "c" };

        int index = ItemSelectionResolver.ResolveIndex(items, "b", 0, value => value);

        Assert.AreEqual(1, index);
    }

    [Test]
    public void ResolveIndex_WhenSelectedItemWasRemoved_UsesNextAtOriginalIndex()
    {
        List<string> items = new List<string> { "a", "c", "d" };

        int index = ItemSelectionResolver.ResolveIndex(items, "b", 1, value => value);

        Assert.AreEqual(1, index);
        Assert.AreEqual("c", items[index]);
    }

    [Test]
    public void ResolveIndex_WhenLastItemWasRemoved_UsesPreviousItem()
    {
        List<string> items = new List<string> { "a", "b" };

        int index = ItemSelectionResolver.ResolveIndex(items, "c", 2, value => value);

        Assert.AreEqual(1, index);
        Assert.AreEqual("b", items[index]);
    }

    [Test]
    public void ResolveIndex_WhenListIsEmpty_ReturnsMinusOne()
    {
        int index = ItemSelectionResolver.ResolveIndex(
            new List<string>(),
            "missing",
            0,
            value => value);

        Assert.AreEqual(-1, index);
    }

    [Test]
    public void ViewState_FeedbackPersistsUntilExplicitlyCleared()
    {
        ShopViewState state = new ShopViewState();

        state.SetFeedback(LocalizedMessage.Ui("shop.feedback.buy_succeeded", "测试物品"));

        Assert.IsTrue(state.HasFeedback);
        Assert.AreEqual("shop.feedback.buy_succeeded", state.Feedback.EntryKey);

        state.ClearFeedback();

        Assert.IsFalse(state.HasFeedback);
    }
}
