using System;
using System.Collections.Generic;
using NutSort.Content;
using UnityEngine;
namespace NutSort.UI
{
    public sealed class OriginalSimpleMarquee:MonoBehaviour
    {
        [SerializeField] private OriginalMarqueeItemView PMDItem;
        [SerializeField] private RectTransform ScrollView;
        [SerializeField] private float stopTime,scrollSpeed,rowHeight;
        private List<OriginalMarqueeItemView> items;
        private Func<OriginalMarqueeItem> select;
        private Action<OriginalMarqueeItemView> bind;
        private float remaining;
        private bool started;
        private int level;
        public IReadOnlyList<OriginalMarqueeItemView> Items=>items;
        public float Remaining=>remaining;
        public void Bind(Func<OriginalMarqueeItem> select,Action<OriginalMarqueeItemView> bind)
        {this.select=select;this.bind=bind;}
        public void Init(int value)
        {
            level=value;transform.localPosition=Vector3.zero;
            gameObject.SetActive(select()!=null);started=true;
            if(items==null)
            {
                Vector2 position=PMDItem.Rect.anchoredPosition;position.y=0;PMDItem.Rect.anchoredPosition=position;
                items=new List<OriginalMarqueeItemView>{PMDItem};
                for(int i=2;i<4;i++)
                {
                    var copy=Instantiate(PMDItem,ScrollView,false);copy.name=i.ToString();
                    // Native code moves the source after cloning, not the newly made copy.
                    position=PMDItem.Rect.anchoredPosition;position.y=-(i-1)*rowHeight;PMDItem.Rect.anchoredPosition=position;
                    items.Add(copy);
                }
                foreach(var item in items){bind(item);SetText(item);}
            }
            remaining=stopTime;
        }
        public void InitUnavailable(string message)
        {
            transform.localPosition=Vector3.zero;
            PMDItem.Tip.text=message;PMDItem.HeadImage.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }
        private void SetText(OriginalMarqueeItemView item)
        {
            OriginalMarqueeItem data=select();
            if(data!=null)item.Init(data,level);
        }
        private void Update()=>Advance(Time.deltaTime);
        public void Advance(float delta)
        {
            if(!started)return;
            remaining-=delta;if(remaining>=0)return;
            float distance=delta*scrollSpeed;
            foreach(var item in items)
            {
                Vector2 position=item.Rect.anchoredPosition;float x=position.x;
                position.y+=distance;item.Rect.anchoredPosition=position;
                if(item.Rect.anchoredPosition.y<rowHeight)continue;
                item.Rect.anchoredPosition=new Vector2(x,-2*rowHeight);
                remaining=stopTime;SetText(item);
            }
        }
    }
}
