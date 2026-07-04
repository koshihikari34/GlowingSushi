using System.Collections.Generic;
using GlowingSushi.ViewModel;
using ObservableCollections;
using R3;
using UnityEngine;
using VContainer;

namespace GlowingSushi.View
{
    /// <summary>
    /// 群れ全体のView。SushiSchoolViewModelのコレクション変更を購読して
    /// SushiViewの生成・破棄を行う。
    /// </summary>
    public sealed class SushiSchoolView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("生成する寿司のプレハブ")]
        SushiView sushiPrefab;

        SushiSchoolViewModel viewModel;
        readonly Dictionary<SushiViewModel, SushiView> views = new();

        [Inject]
        public void Construct(SushiSchoolViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        void Start()
        {
            // 既に存在する個体分のViewを生成してから、以降の増減を購読する
            foreach (var sushi in viewModel.Sushis)
            {
                AddView(sushi);
            }

            viewModel.Sushis.ObserveAdd()
                .Subscribe(addEvent => AddView(addEvent.Value))
                .AddTo(this);

            viewModel.Sushis.ObserveRemove()
                .Subscribe(removeEvent => RemoveView(removeEvent.Value))
                .AddTo(this);
        }

        void AddView(SushiViewModel sushi)
        {
            if (views.ContainsKey(sushi)) return;
            var view = Instantiate(sushiPrefab, sushi.Position.Value, sushi.Rotation.Value, transform);
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
