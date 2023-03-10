using UnityEngine;
using Verse;

namespace FireControl;

public static class UIUtility
{
    public static Rect TakeTopPart(ref this Rect rect, float pixels)
    {
        var ret = rect.TopPartPixels(pixels);
        rect.yMin += pixels;
        return ret;
    }

    public static Rect TakeBottomPart(ref this Rect rect, float pixels)
    {
        var ret = rect.BottomPartPixels(pixels);
        rect.yMax -= pixels;
        return ret;
    }

    public static Rect TakeRightPart(ref this Rect rect, float pixels)
    {
        var ret = rect.RightPartPixels(pixels);
        rect.xMax -= pixels;
        return ret;
    }

    public static Rect TakeLeftPart(ref this Rect rect, float pixels)
    {
        var ret = rect.LeftPartPixels(pixels);
        rect.xMin += pixels;
        return ret;
    }

    public static Rect Center(this Rect rect, Vector2 size) => new Rect(Vector2.zero, size).CenteredOnXIn(rect).CenteredOnYIn(rect);

    public static Rect Center(this Rect rect, float x, float y) => new Rect(0, 0, x, y).CenteredOnXIn(rect).CenteredOnYIn(rect);

    public static void DrawLineAcross(this Rect rect)
    {
        Widgets.DrawLineHorizontal(rect.x, rect.y + rect.height / 2f, rect.width);
    }
}
