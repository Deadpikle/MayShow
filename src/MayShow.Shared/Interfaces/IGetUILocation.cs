using MayShow.Enums;

#if IOS
using Microsoft.Maui.Graphics;
#endif

namespace MayShow.Interfaces;

interface IGetUILocation
{
    #if IOS
    Rect GetUILocation(UIItem item);
    #endif
}