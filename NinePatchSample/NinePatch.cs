using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections;

namespace  NinePatchSample
{
    /// <summary>
    /// C#でAndroid 9-patchを扱う為のクラス
    /// Androidでは下辺と右辺で内部領域を指定するが、
    /// このクラスでは単純に上辺と左辺(どこが引き延ばすピクセルか)のみを用いる
    /// 
    /// 使い方：
    /// 
    /// NinePatch ninePatch = new NinePatch(image); // 9-patch画像imageから9-patchオブジェクトを作成
    /// Image newImage = ninePatch.ImageSizeOf(500, 500) // 500x500に引き伸ばした画像を取得
    /// 
    /// </summary>
    public class NinePatch
    {
        /// <summary>
        /// 上下左右1ピクセル付の9-patch画像
        /// </summary>
        private Image image;

        /// <summary>
        /// 上下左右1ピクセルを除いたオリジナル画像を返す
        /// </summary>
        public Bitmap OriginalImage
        {
            get {
                using (Bitmap baseBitmap = new Bitmap(this.image))
                {
                    Rectangle rect = new Rectangle(1, 1, baseBitmap.Width - 2, baseBitmap.Height - 2);
                    Bitmap result = baseBitmap.Clone(rect, baseBitmap.PixelFormat);
                    return result;
                }
            }
        }

        /// <summary>
        /// 画像サイズごとの画像キャッシュ
        /// </summary>
        private Dictionary<string, Image> cache;

        /// <summary>
        /// 上辺パッチ
        /// </summary>
        private List<int> topPatches;

        /// <summary>
        /// 左辺パッチ
        /// </summary>
        private List<int> leftPatches;

        /// <summary>
        /// 下辺パッチ
        /// </summary>
        private List<int> bottomPatches;

        /// <summary>
        /// 右辺パッチ
        /// </summary>
        private List<int> rightPatches;

        /// <summary>
        /// ピクセルごとのバイト数
        /// </summary>
        private const int BYTES_PER_PIXEL = 4;

        public NinePatch(Image image)
        {
            this.image = image;
            cache = new Dictionary<string, Image>();
            FindPatchRegion();
        }

        /// <summary>
        /// 画像キャッシュをクリアする
        /// </summary>
        public void ClearCache()
        {
            foreach (KeyValuePair<string, Image>pair in cache)
            {
                pair.Value.Dispose();
            }
            cache.Clear();
        }

        /// <summary>
        /// 縦横のサイズを指定して、そのサイズに引き延ばした画像オブジェクトを作成する
        /// </summary>
        /// <param name="w">得たい画像オブジェクトの幅</param>
        /// <param name="h">得たい画像オブジェクトの高さ</param>
        /// <returns>画像オブジェクト</returns>
        public Image ImageSizeOf(int w, int h)
        {
            /*
             * 同一サイズの画像オブジェクトが生成済の場合、キャッシュとして保持してあるものを返す
             */
            if (cache.ContainsKey(String.Format("{0}x{1}", w, h)))
            {
                return cache[String.Format("{0}x{1}", w, h)];
            }

            using (Bitmap src = this.OriginalImage)
            {
                int sourceWidth = src.Width;
                int sourceHeight = src.Height;
                int targetWidth = w;
                int targetHeight = h;

                // sourceよりも小さいtargetサイズが指定された場合は、強制的に
                // sourceのサイズにしてしまう。
                // sourceとtargetのサイズが同じだったら、オリジナル画像をそのまま返す
                targetWidth = System.Math.Max(sourceWidth, targetWidth);
                targetHeight = System.Math.Max(sourceHeight, targetHeight);
                if (sourceWidth == targetWidth && sourceHeight == targetHeight)
                {
                    return src;
                }

                // source画像のbufferを用意
                BitmapData srcData = src.LockBits(
                    new Rectangle(0, 0, sourceWidth, sourceHeight),
                    ImageLockMode.ReadOnly,
                    src.PixelFormat
                );
                byte[] srcBuf = new byte[sourceWidth * sourceHeight * BYTES_PER_PIXEL];
                Marshal.Copy(srcData.Scan0, srcBuf, 0, srcBuf.Length);

                // target画像のbufferを用意
                Bitmap dst = new Bitmap(targetWidth, targetHeight);
                byte[] dstBuf = new byte[dst.Width * dst.Height * BYTES_PER_PIXEL];

                // x座標、y座標それぞれについて、「targetの各座標がsourceのどの座標に対応するか」を取得
                List<int> xMapping = XMapping(targetWidth - sourceWidth, targetWidth);
                List<int> yMapping = YMapping(targetHeight - sourceHeight, targetHeight);

                // 上記マッピングに従って各ピクセルの値をコピー
                for (int y = 0; y < targetHeight; y++)
                {
                    int sourceY = yMapping[y];
                    for (int x = 0; x < targetWidth; x++)
                    {
                        int sourceX = xMapping[x];

                        for (int z = 0; z < BYTES_PER_PIXEL; z++)
                        {
                            dstBuf[y * targetWidth * BYTES_PER_PIXEL + x * BYTES_PER_PIXEL + z] =
                                srcBuf[sourceY * sourceWidth * BYTES_PER_PIXEL + sourceX * BYTES_PER_PIXEL + z];
                        }
                    }
                }

                // target画像をBitmapにコピー
                BitmapData dstData = dst.LockBits(
                    new Rectangle(0, 0, dst.Width, dst.Height),
                    ImageLockMode.WriteOnly,
                    src.PixelFormat
                );
                IntPtr dstScan0 = dstData.Scan0;
                Marshal.Copy(dstBuf, 0, dstScan0, dstBuf.Length);

                src.UnlockBits(srcData);
                dst.UnlockBits(dstData);
                
                // サイズとともにキャッシュに保存
                cache.Add(String.Format("{0}x{1}", w, h), dst);

                return dst;
            }
        }

        /// <summary>
        /// 9-patch画像の上下左右1ピクセルから、
        /// どこが引き伸ばし範囲に該当するかを記憶しておく
        /// </summary>
        private void FindPatchRegion()
        {
            topPatches = new List<int>();
            leftPatches = new List<int>();
            bottomPatches = new List<int>();
            rightPatches = new List<int>();

            using (Bitmap src = new Bitmap(image))
            {
                BitmapData srcData = src.LockBits(
                    new Rectangle(0, 0, src.Width, src.Height),
                    ImageLockMode.ReadOnly,
                    src.PixelFormat
                );
                byte[] srcBuf = new byte[src.Width * src.Height * BYTES_PER_PIXEL];
                Marshal.Copy(srcData.Scan0, srcBuf, 0, srcBuf.Length);

                // top
                for (int x = 1; x < srcData.Width-1; x++)
                {
                    int index = x * BYTES_PER_PIXEL;
                    byte b     = srcBuf[index];
                    byte g     = srcBuf[index + 1];
                    byte r     = srcBuf[index + 2];
                    byte alpha = srcBuf[index + 3];
                    if (r == 0 && g == 0 && b == 0 && alpha == 255)
                    {
                        topPatches.Add(x-1);
                    }
                }

                // left
                for (int y = 1; y < srcData.Height-1; y++)
                {
                    int index = y * BYTES_PER_PIXEL * srcData.Width;
                    byte b = srcBuf[index];
                    byte g = srcBuf[index + 1];
                    byte r = srcBuf[index + 2];
                    byte alpha = srcBuf[index + 3];
                    if (r == 0 && g == 0 && b == 0 && alpha == 255)
                    {
                        leftPatches.Add(y-1);
                    }
                }

                // bottom
                for (int x = 1; x < srcData.Width - 1; x++)
                {
                    int index = (srcData.Height-1) * BYTES_PER_PIXEL * srcData.Width + x * BYTES_PER_PIXEL;
                    byte b = srcBuf[index];
                    byte g = srcBuf[index + 1];
                    byte r = srcBuf[index + 2];
                    byte alpha = srcBuf[index + 3];
                    if (r == 0 && g == 0 && b == 0 && alpha == 255)
                    {
                        bottomPatches.Add(x - 1);
                    }
                }

                // right
                for (int y = 1; y < srcData.Height - 1; y++)
                {
                    int index = y * BYTES_PER_PIXEL * srcData.Width + (srcData.Width-1) * BYTES_PER_PIXEL;
                    byte b = srcBuf[index];
                    byte g = srcBuf[index + 1];
                    byte r = srcBuf[index + 2];
                    byte alpha = srcBuf[index + 3];
                    if (r == 0 && g == 0 && b == 0 && alpha == 255)
                    {
                        rightPatches.Add(y - 1);
                    }
                }

            }
        }

        /// <summary>
        /// 生成したい画像のx座標が、オリジナル画像のどこの座標に対応するかを
        /// 表したリストを取得する
        /// </summary>
        /// <param name="diffWidth">生成したい画像とオリジナル画像の、幅の差</param>
        /// <param name="targetWidth">生成したい画像の幅</param>
        /// <returns></returns>
        private List<int> XMapping(int diffWidth, int targetWidth)
        {
            List<int> result = new List<int>(targetWidth);
            int src = 0;
            int dst = 0;
            while (dst < targetWidth)
            {
                int foundIndex = topPatches.IndexOf(src);
                if (foundIndex != -1)
                {
                    int repeatCount = (diffWidth / topPatches.Count) + 1;
                    if (foundIndex < diffWidth % topPatches.Count)
                    {
                        repeatCount++;
                    }
                    for (int j = 0; j < repeatCount; j++)
                    {
                        result.Insert(dst++, src);
                    }
                }
                else
                {
                    result.Insert(dst++, src);
                }
                src++;
            }
            return result;
        }

        /// <summary>
        /// 生成したい画像のy座標が、オリジナル画像のどこの座標に対応するかを
        /// 表したリストを取得する
        /// </summary>
        /// <param name="diffHeight">生成したい画像とオリジナル画像の、高さの差</param>
        /// <param name="targetHeight">生成したい画像の高さ</param>
        /// <returns></returns>
        private List<int> YMapping(int diffHeight, int targetHeight)
        {
            List<int> result = new List<int>(targetHeight);
            int src = 0;
            int dst = 0;
            while (dst < targetHeight)
            {
                int foundIndex = leftPatches.IndexOf(src);
                if (foundIndex != -1)
                {
                    int repeatCount = (diffHeight / leftPatches.Count) + 1;
                    if (foundIndex < diffHeight % leftPatches.Count)
                    {
                        repeatCount++;
                    }
                    for (int j = 0; j < repeatCount; j++)
                    {
                        result.Insert(dst++, src);
                    }
                }
                else
                {
                    result.Insert(dst++, src);
                }
                src++;
            }
            return result;
        }
    }
}
