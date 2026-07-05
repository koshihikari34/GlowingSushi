using System;
using System.Collections.Generic;
using GlowingSushi.ViewModel;
using ObservableCollections;
using R3;
using UnityEngine;
using VContainer;

namespace GlowingSushi.View
{
    /// <summary>
    /// 表面ふるまいスポット群のView。SurfaceSpotsViewModelのスポット一覧と、
    /// 各スポットの個体一覧の増減を購読してSushiViewを生成・破棄する。
    /// </summary>
    public sealed class SurfaceSpotsView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("生成する寿司のプレハブ(種類ごと。個体生成時にランダムに選ばれる)")]
        SushiView[] sushiPrefabs;

        SurfaceSpotsViewModel viewModel;
        readonly Dictionary<SushiViewModel, SushiView> views = new();
        readonly Dictionary<SurfaceSpotViewModel, IDisposable> spotSubscriptions = new();

        [Inject]
        public void Construct(SurfaceSpotsViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        void Start()
        {
            foreach (var spot in viewModel.Spots)
            {
                AddSpot(spot);
            }

            viewModel.Spots.ObserveAdd()
                .Subscribe(addEvent => AddSpot(addEvent.Value))
                .AddTo(this);

            viewModel.Spots.ObserveRemove()
                .Subscribe(removeEvent => RemoveSpot(removeEvent.Value))
                .AddTo(this);
        }

        void OnDestroy()
        {
            foreach (var subscription in spotSubscriptions.Values)
            {
                subscription.Dispose();
            }
            spotSubscriptions.Clear();
        }

        void AddSpot(SurfaceSpotViewModel spot)
        {
            if (spotSubscriptions.ContainsKey(spot)) return;

            foreach (var sushi in spot.Sushis)
            {
                AddView(sushi);
            }

            var addSub = spot.Sushis.ObserveAdd()
                .Subscribe(addEvent => AddView(addEvent.Value));
            var removeSub = spot.Sushis.ObserveRemove()
                .Subscribe(removeEvent => RemoveView(removeEvent.Value));
            spotSubscriptions.Add(spot, Disposable.Combine(addSub, removeSub));
        }

        void RemoveSpot(SurfaceSpotViewModel spot)
        {
            if (spotSubscriptions.Remove(spot, out var subscription))
            {
                subscription.Dispose();
            }
            foreach (var sushi in spot.Sushis)
            {
                RemoveView(sushi);
            }
        }

        void AddView(SushiViewModel sushi)
        {
            if (views.ContainsKey(sushi)) return;
            var prefab = sushiPrefabs[UnityEngine.Random.Range(0, sushiPrefabs.Length)];
            var view = Instantiate(prefab, sushi.Position.Value, sushi.Rotation.Value, transform);
            view.Bind(sushi);
            views.Add(sushi, view);
        }

        void RemoveView(SushiViewModel sushi)
        {
            if (!views.Remove(sushi, out var view)) return;
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }
    }
}
