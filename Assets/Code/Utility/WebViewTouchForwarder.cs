using UnityEngine;
using UnityEngine.EventSystems;
using Vuplex.WebView;

public class WebViewTouchForwarder : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IDragHandler {

    IWithTouch _touchWebView;

    void Awake() {
        _touchWebView = GetComponentInChildren<IWithTouch>();
    }

    public void OnPointerDown(PointerEventData eventData) {
        _touchWebView.SendTouchEvent(new TouchEvent {
            Type  = TouchEventType.Start,
            Point = eventData.position
        });
    }

    public void OnPointerUp(PointerEventData eventData) {
        _touchWebView.SendTouchEvent(new TouchEvent {
            Type  = TouchEventType.End,
            Point = eventData.position
        });
    }

    public void OnDrag(PointerEventData eventData) {
        _touchWebView.SendTouchEvent(new TouchEvent {
            Type  = TouchEventType.Move,
            Point = eventData.position
        });
    }
}