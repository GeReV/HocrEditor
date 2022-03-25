using System.Runtime.InteropServices;

namespace HocrEditor.Tesseract;

// struct Pix
// {
//     l_uint32             w;           /* width in pixels                   */
//     l_uint32             h;           /* height in pixels                  */
//     l_uint32             d;           /* depth in bits                     */
//     l_uint32             wpl;         /* 32-bit words/line                 */
//     l_uint32             refcount;    /* reference count (1 if no clones)  */
//     l_int32              xres;        /* image res (ppi) in x direction    */
//                                       /* (use 0 if unknown)                */
//     l_int32              yres;        /* image res (ppi) in y direction    */
//                                       /* (use 0 if unknown)                */
//     l_int32              informat;    /* input file format, IFF_*          */
//     char                *text;        /* text string associated with pix   */
//     struct PixColormap  *colormap;    /* colormap (may be null)            */
//     l_uint32            *data;        /* the image data                    */
// };

[StructLayout(LayoutKind.Sequential)]
public struct Pix
{
    public uint w;
    public uint h;
    public uint d;
    public uint wpl;
    public uint refcount;
    public int xres;
    public int yres;
    public int informat;
    public IntPtr text;
    public IntPtr colormap;
    public IntPtr unknown;
    public IntPtr data;
}
