using System.Collections.Generic;
using GlowingSushi.ViewModel;
using ObservableCollections;
using R3;
using UnityEngine;
using VContainer;

namespace GlowingSushi.View
{
    /// <summary>
    /// 水族館全体のView。AquariumViewModelの群れ一覧と、
    /// 各群れの個体一覧の増減を購読してSushiViewを生成・破棄する。
    /// </summary>
    public sealed class AquariumView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("生成する寿司のプレハブ(種類ごと。個体生成時にランダムに選ばれる)")]
        SushiView[] sushiPrefabs;

        AquariumViewModel viewModel;
        readonly Dictionary<SushiViewModel, SushiView> views = new();
        readonly Dictionary<SushiSchoolViewModel, IDisposable> schoolSubscriptions = new();

        [Inject]
        public void Construct(AquariumViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        void Start()
        {
            // 既に存在する群れ分を反映してから、以降の増減を購読する
            foreach (var school in viewModel.Schools)
            {
                AddSchool(school);
            }

            viewModel.Schools.ObserveAdd()
                .Subscribe(addEvent => AddSchool(addEvent.Value))
                .AddTo(this);

            viewModel.Schools.ObserveRemove()
                .Subscribe(removeEvent => RemoveSchool(removeEvent.Value))
                .AddTo(this);
        }

        void OnDestroy()
        {
            foreach (var subscription in schoolSubscriptions.Values)
            {
                subscription.Dispose();
            }
            schoolSubscriptions.Clear();
        }

        /// <summary>群れ1つ分: 既存個体のViewを生成し、以降の個体増減を購読する</summary>
        void AddSchool(SushiSchoolViewModel school)
        {
            if (schoolSubscriptions.ContainsKey(school)) return;

            foreach (var sushi in school.Sushis)
            {
                AddView(sushi);
            }

            var addSub = school.Sushis.ObserveAdd()
                .Subscribe(addEvent => AddView(addEvent.Value));
            var removeSub = school.Sushis.ObserveRemove()
                .Subscribe(removeEvent => RemoveView(removeEvent.Value));
            schoolSubscriptions.Add(school, Disposable.Combine(addSub, removeSub));
        }

        void RemoveSchool(SushiSchoolViewModel school)
        {
            if (schoolSubscriptions.Remove(school, out var subscription))
            {
                subscription.Dispose();
            }
            foreach (var sushi in school.Sushis)
            {
                RemoveView(sushi);
            }
        }

        void AddView(SushiViewModel sushi)
        {
            if (views.ContainsKey(sushi)) return;
            // 寿司の種類(見た目)はランダムに選ぶ
            var prefab = sushiPrefabs[Random.Range(0, sushiPrefabs.Length)];
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
