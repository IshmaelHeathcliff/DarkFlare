using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public enum ShopFocusTarget
    {
        Item,
        BuyAction,
        SellAction,
        CloseFallback
    }

    public sealed class ItemListViewState
    {
        public string SelectedInstanceId { get; set; } = string.Empty;

        public int FallbackIndex { get; set; }

        public void Reset()
        {
            SelectedInstanceId = string.Empty;
            FallbackIndex = 0;
        }
    }

    public sealed class ShopViewState
    {
        public ShopItemSource ActiveSource { get; set; } = ShopItemSource.Merchant;

        public ItemListViewState Merchant { get; } = new ItemListViewState();

        public ItemListViewState Player { get; } = new ItemListViewState();

        public ShopFocusTarget FocusTarget { get; set; } = ShopFocusTarget.Item;

        public string Feedback { get; private set; } = string.Empty;

        public bool HasFeedback => !string.IsNullOrWhiteSpace(Feedback);

        public ItemListViewState GetList(ShopItemSource source)
        {
            return source == ShopItemSource.Merchant ? Merchant : Player;
        }

        public void SetFeedback(string feedback)
        {
            Feedback = feedback ?? string.Empty;
        }

        public void ClearFeedback()
        {
            Feedback = string.Empty;
        }

        public void Reset()
        {
            ActiveSource = ShopItemSource.Merchant;
            Merchant.Reset();
            Player.Reset();
            FocusTarget = ShopFocusTarget.Item;
            ClearFeedback();
        }
    }

    public static class ItemSelectionResolver
    {
        public static int ResolveIndex<T>(
            IReadOnlyList<T> items,
            string selectedInstanceId,
            int fallbackIndex,
            Func<T, string> getInstanceId)
        {
            if (items == null || items.Count == 0)
            {
                return -1;
            }

            if (!string.IsNullOrWhiteSpace(selectedInstanceId))
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (getInstanceId(items[i]) == selectedInstanceId)
                    {
                        return i;
                    }
                }
            }

            return Mathf.Clamp(fallbackIndex, 0, items.Count - 1);
        }
    }
}
