using System;
using NutSort.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // Author static hierarchy once; the player only instantiates this saved prefab.
    public static class LocalWithdrawalConditionsBuilder
    {
        public static void Run()
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/Resources/prefabs/panels/TXPanel.prefab");
            try
            {
                var original=root.GetComponent<OriginalWithdrawalPanel>();
                var source=new SerializedObject(original);
                var main=(RectTransform)source.FindProperty("main").objectReferenceValue;
                var title=(TMP_Text)source.FindProperty("Title").objectReferenceValue;
                var action=(Button)source.FindProperty("TXBtn").objectReferenceValue;
                var close=(Button)source.FindProperty("Close").objectReferenceValue;
                var caption=(TMP_Text)source.FindProperty("TXText").objectReferenceValue;
                var textSample=(TMP_Text)source.FindProperty("Tip").objectReferenceValue;
                var font=textSample.font;var material=textSample.fontSharedMaterial;
                // Retain only the frame, heading and standard Buttons. The monetary
                // card and its static "Level 3" label belong to the source cash page.
                foreach(Transform child in main)
                {
                    bool frame=child.name=="Image"&&Mathf.Abs(((RectTransform)child).anchoredPosition.y-231)<1;
                    bool interior=child.name=="Image (1)";
                    child.gameObject.SetActive(frame||interior||child.name=="Title1"||child==close.transform||child==action.transform);
                    if(interior){var rect=(RectTransform)child;rect.anchoredPosition=new Vector2(0,285);rect.sizeDelta=new Vector2(881,840);}
                }
                foreach(string property in new[]{"Level1","Level2","GoldCount","Progress","ProgressValue","ProgressTip","PlayerInfoPos"})
                {var obj=source.FindProperty(property).objectReferenceValue as Component;if(obj!=null)obj.gameObject.SetActive(false);else{var go=source.FindProperty(property).objectReferenceValue as GameObject;if(go!=null)go.SetActive(false);}}
                var goldImage=source.FindProperty("goldImage").objectReferenceValue as Component;
                if(goldImage!=null)goldImage.gameObject.SetActive(false);
                textSample.gameObject.SetActive(false);
                main.localScale=Vector3.one;
                var body=Rect("Requirements",main,new Vector2(0,250),new Vector2(880,820));
                var subtitle=Label("Subtitle",body,font,material,new Vector2(0,405),new Vector2(820,60),36,TextAlignmentOptions.Center);
                var notice=Label("Notice",body,font,material,new Vector2(0,330),new Vector2(820,80),30,TextAlignmentOptions.Center);
                var controller=root.AddComponent<LocalWithdrawalConditionsPanel>();
                var serialized=new SerializedObject(controller);
                Set(serialized,"title",title);Set(serialized,"subtitle",subtitle);Set(serialized,"notice",notice);Set(serialized,"actionCaption",caption);Set(serialized,"action",action);Set(serialized,"close",close);
                var rows=serialized.FindProperty("rows");rows.arraySize=7;
                for(int i=0;i<7;i++)
                {
                    var row=Rect("Requirement"+i,body,new Vector2(0,230-i*82),new Vector2(840,76));
                    var background=row.gameObject.AddComponent<Image>();background.color=new Color(1,.98f,.90f,.82f);background.raycastTarget=false;
                    var name=Label("Name",row,font,material,new Vector2(-145,14),new Vector2(520,34),27,TextAlignmentOptions.MidlineLeft);
                    var value=Label("Value",row,font,material,new Vector2(-145,-20),new Vector2(520,32),24,TextAlignmentOptions.MidlineLeft);
                    var status=Label("Status",row,font,material,new Vector2(290,0),new Vector2(240,60),25,TextAlignmentOptions.Center);
                    var item=rows.GetArrayElementAtIndex(i);item.FindPropertyRelative("Root").objectReferenceValue=row.gameObject;item.FindPropertyRelative("Name").objectReferenceValue=name;item.FindPropertyRelative("Value").objectReferenceValue=value;item.FindPropertyRelative("Status").objectReferenceValue=status;
                }
                serialized.FindProperty("completeColor").colorValue=new Color(.1f,.45f,.15f);
                serialized.FindProperty("pendingColor").colorValue=new Color(.65f,.22f,.05f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                // Existing Button visuals and close target retain their prefab ownership.
                action.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-175);
                action.GetComponent<RectTransform>().sizeDelta=new Vector2(720,140);
                caption.enableAutoSizing=true;caption.fontSizeMin=30;caption.fontSizeMax=55;
                title.text="Withdrawal requirements";title.enableAutoSizing=true;title.fontSizeMin=32;
                UnityEngine.Object.DestroyImmediate(original);
                root.name="LocalWithdrawalConditions";
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/prefabs/panels/LocalWithdrawalConditions.prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
            Debug.Log("NUT_WITHDRAWAL_CONDITIONS_PREFAB_PASS");
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;
            var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        private static TMP_Text Label(string name,Transform parent,TMP_FontAsset font,Material material,Vector2 position,Vector2 size,float fontSize,TextAlignmentOptions alignment)
        {
            var rect=Rect(name,parent,position,size);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSharedMaterial=material;text.fontSize=fontSize;text.alignment=alignment;text.color=new Color(.28f,.15f,.08f);text.raycastTarget=false;text.enableAutoSizing=true;text.fontSizeMin=20;text.fontSizeMax=fontSize;return text;
        }
        private static void Set(SerializedObject owner,string property,UnityEngine.Object value)=>owner.FindProperty(property).objectReferenceValue=value;
    }
}
