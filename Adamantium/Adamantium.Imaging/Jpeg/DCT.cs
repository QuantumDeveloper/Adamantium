/// Copyright (c) 2008 Jeffrey Powers for Fluxcapacity Open Source.
/// Under the MIT License, details: License.txt..

// NOTE: Compile with DYNAMIC_IDCT for a decode performance boost.
//       May not yield a perceptible boost for small images,
//       since there is some overhead in emitting CIL dynamically.

using System;

#if DYNAMIC_IDCT
using System.Reflection.Emit;
using System.Reflection;
#endif

namespace Adamantium.Imaging.Jpeg
{
    /// <summary>
    /// Implements the Discrete Cosine Transform with dynamic CIL
    /// </summary>
    internal partial class DCT
    {
        private float[] _temp = new float[64];

        // Cosine matrix and transposed cosine matrix
        private static readonly float[,] c = buildC();
        private static readonly float[,] cT = buildCT();

        // The same two tables, flat. Built once from the square ones so there is a single definition of the numbers.
        private static readonly float[] cFlat = Flatten(c);
        private static readonly float[] cTFlat = Flatten(cT);

        private static float[] Flatten(float[,] square)
        {
            var flat = new float[64];
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    flat[i * 8 + j] = square[i, j];
            return flat;
        }

        internal DCT()
        {
#if DYNAMIC_IDCT
            dynamicIDCT = dynamicIDCT ?? EmitIDCT();
#endif
        }

        /// <summary>
        /// Precomputes cosine terms in A.3.3 of 
        /// http://www.w3.org/Graphics/JPEG/itu-t81.pdf
        /// 
        /// Closely follows the term precomputation in the
        /// Java Advanced Imaging library.
        /// </summary>
        private static float[,] buildC()
        {
            float[,] c = new float[8, 8];

            for (int i = 0; i < 8; i++) // i == u or v
            {
                for (int j = 0; j < 8; j++) // j == x or y
                {
                    c[i, j] = i == 0 ?
                        0.353553391f : /* 1 / SQRT(8) */
                        (float)(0.5 * Math.Cos((2.0 * j + 1) * i * Math.PI / 16.0));
                }
            }

            return c;
        }
        private static float[,] buildCT()
        {
            // Transpose i,k <-- j,i
            float[,] cT = new float[8, 8];
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    cT[j, i] = c[i, j];
            return cT;
        }

        public static void SetValueClipped(byte[,] arr, int i, int j, float val)
        {
            // Clip into the 0...255 range & round
            arr[i, j] = val < 0 ? (byte)0
                : val > 255 ? (byte)255
                : (byte)(val + 0.5);
        }

        /// See figure A.3.3 IDCT (informative) on A-5.
        /// http://www.w3.org/Graphics/JPEG/itu-t81.pdf
        // AAN scale factors. The fast transform below computes a SCALED inverse DCT, so every coefficient is first
        // multiplied by aan[row] * aan[col] / 8 to bring the result back to the same numbers the direct form produces.
        // libjpeg folds these into the quantization table; this decoder dequantizes in a separate pass, so they live
        // here as one flat table instead - 64 multiplies against the ~900 the direct form costs.
        private static readonly float[] aanScale = BuildAanScale();

        private static float[] BuildAanScale()
        {
            var aan = new[]
            {
                1.0f, 1.387039845f, 1.306562965f, 1.175875602f,
                1.0f, 0.785694958f, 0.541196100f, 0.275899379f
            };

            var scale = new float[64];
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                    scale[row * 8 + col] = aan[row] * aan[col] / 8.0f;

            return scale;
        }

        /// <summary>
        /// The inverse DCT as two passes of the Arai-Agui-Nakajima factorisation - columns, then rows.
        ///
        /// <para>Same result as <see cref="ReferenceIDCT"/>, which is what it replaced, at a fraction of the cost: the
        /// direct form is two 8x8x8 matrix multiplies, 1024 multiplies for every block, and a 4K photograph has some
        /// 400 000 blocks in each of three components. Measured on one: 3.9 seconds of a 6.4 second decode was this
        /// function. The factorisation gets the same numbers out with about a tenth of the arithmetic by sharing
        /// subexpressions between outputs rather than recomputing each one from scratch.</para>
        ///
        /// <para>The all-zero shortcut matters as much as the arithmetic. Most blocks in a photograph carry only a DC
        /// term and a few low frequencies, so most columns are entirely zero above the first row - and a zero column
        /// transforms to a constant, which needs no transform at all.</para>
        /// </summary>
        private static void AanIDCT(float[] input, float[] work, byte[] output, int offset)
        {
            const float Sqrt2 = 1.414213562f;      // 2 * cos(pi/4)
            const float C6 = 1.847759065f;         // 2 * cos(pi/8) ... the AAN constants, named as libjpeg names them
            const float C2mC6 = 1.082392200f;
            const float mC2mC6 = -2.613125930f;

            // Pass 1: columns.
            for (int col = 0; col < 8; col++)
            {
                // A column with nothing but its DC term is a constant column. Cheapest possible answer, and the common
                // case - skipping it is worth more here than any instruction-level tuning of the rest.
                if (input[col + 8] == 0 && input[col + 16] == 0 && input[col + 24] == 0 && input[col + 32] == 0
                    && input[col + 40] == 0 && input[col + 48] == 0 && input[col + 56] == 0)
                {
                    var dc = input[col] * aanScale[col];
                    work[col] = dc;
                    work[col + 8] = dc;
                    work[col + 16] = dc;
                    work[col + 24] = dc;
                    work[col + 32] = dc;
                    work[col + 40] = dc;
                    work[col + 48] = dc;
                    work[col + 56] = dc;
                    continue;
                }

                var t0 = input[col] * aanScale[col];
                var t1 = input[col + 16] * aanScale[col + 16];
                var t2 = input[col + 32] * aanScale[col + 32];
                var t3 = input[col + 48] * aanScale[col + 48];

                var t10 = t0 + t2;
                var t11 = t0 - t2;
                var t13 = t1 + t3;
                var t12 = (t1 - t3) * Sqrt2 - t13;

                t0 = t10 + t13;
                t3 = t10 - t13;
                t1 = t11 + t12;
                t2 = t11 - t12;

                var t4 = input[col + 8] * aanScale[col + 8];
                var t5 = input[col + 24] * aanScale[col + 24];
                var t6 = input[col + 40] * aanScale[col + 40];
                var t7 = input[col + 56] * aanScale[col + 56];

                var z13 = t6 + t5;
                var z10 = t6 - t5;
                var z11 = t4 + t7;
                var z12 = t4 - t7;

                t7 = z11 + z13;
                t11 = (z11 - z13) * Sqrt2;

                var z5 = (z10 + z12) * C6;
                t10 = C2mC6 * z12 - z5;
                t12 = mC2mC6 * z10 + z5;

                t6 = t12 - t7;
                t5 = t11 - t6;
                t4 = t10 + t5;

                work[col] = t0 + t7;
                work[col + 56] = t0 - t7;
                work[col + 8] = t1 + t6;
                work[col + 48] = t1 - t6;
                work[col + 16] = t2 + t5;
                work[col + 40] = t2 - t5;
                work[col + 32] = t3 + t4;
                work[col + 24] = t3 - t4;
            }

            // Pass 2: rows, with the level shift and the clamp folded into the store.
            for (int row = 0; row < 8; row++)
            {
                int r = row * 8;

                var t10 = work[r] + work[r + 4];
                var t11 = work[r] - work[r + 4];
                var t13 = work[r + 2] + work[r + 6];
                var t12 = (work[r + 2] - work[r + 6]) * Sqrt2 - t13;

                var t0 = t10 + t13;
                var t3 = t10 - t13;
                var t1 = t11 + t12;
                var t2 = t11 - t12;

                var z13 = work[r + 5] + work[r + 3];
                var z10 = work[r + 5] - work[r + 3];
                var z11 = work[r + 1] + work[r + 7];
                var z12 = work[r + 1] - work[r + 7];

                var t7 = z11 + z13;
                t11 = (z11 - z13) * Sqrt2;

                var z5 = (z10 + z12) * C6;
                t10 = C2mC6 * z12 - z5;
                t12 = mC2mC6 * z10 + z5;

                var t6 = t12 - t7;
                var t5 = t11 - t6;
                var t4 = t10 + t5;

                var at = offset + r;
                Store(output, at, t0 + t7);
                Store(output, at + 7, t0 - t7);
                Store(output, at + 1, t1 + t6);
                Store(output, at + 6, t1 - t6);
                Store(output, at + 2, t2 + t5);
                Store(output, at + 5, t2 - t5);
                Store(output, at + 4, t3 + t4);
                Store(output, at + 3, t3 - t4);
            }
        }

        // The level shift (+128), the clamp and the rounding, in the one place they belong - the store.
        private static void Store(byte[] output, int index, float value)
        {
            var v = value + 128f;
            if (v < 0) output[index] = 0;
            else if (v > 255) output[index] = 255;
            else output[index] = (byte)(v + 0.5f);
        }

        /// <summary>The direct form this decoder used to run: two matrix multiplies straight from the definition. Kept
        /// as the REFERENCE the fast transform is tested against - it is obviously correct and hopelessly slow, which
        /// is exactly what a reference should be.</summary>
        internal static void ReferenceIDCT(float[] input, float[] work, byte[,] output)
        {
            for (int i = 0; i < 8; i++)
            {
                int row = i * 8;
                for (int j = 0; j < 8; j++)
                {
                    float val = 0;
                    for (int k = 0; k < 8; k++) val += input[row + k] * cFlat[k * 8 + j];
                    work[row + j] = val;
                }
            }

            for (int i = 0; i < 8; i++)
            {
                int row = i * 8;
                for (int j = 0; j < 8; j++)
                {
                    float temp = 128f;
                    for (int k = 0; k < 8; k++) temp += cTFlat[row + k] * work[k * 8 + j];

                    if (temp < 0) output[i, j] = 0;
                    else if (temp > 255) output[i, j] = 255;
                    else output[i, j] = (byte)(temp + 0.5);
                }
            }
        }

        /// <summary>One block, written straight into <paramref name="output"/> at <paramref name="offset"/> as 64
        /// bytes in row order. No array is returned - allocating one per block meant hundreds of thousands of objects
        /// for a single photograph, all alive at once until the raster was written.</summary>
        internal void FastIDCT(float[] input, byte[] output, int offset)
        {
            AanIDCT(input, _temp, output, offset);
        }



#if DYNAMIC_IDCT

        /// <summary>
        /// Generates a pure-IL nonbranching stream of instructions
        /// that perform the inverse DCT.  Relies on helper function
        /// SetValueClipped.
        /// </summary>
        /// <returns>A delegate to the DynamicMethod</returns>
        private static IDCTFunc EmitIDCT()
        {
            Type[] args = { typeof(float[]), typeof(float[]), typeof(byte[,]) };

            DynamicMethod idctMethod = new DynamicMethod("dynamicIDCT",
                null,        // no return type
                args); // input arrays

            ILGenerator il = idctMethod.GetILGenerator();

            int idx = 0;

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    il.Emit(OpCodes.Ldarg_1);                           // 1  {temp}
                    il.Emit(OpCodes.Ldc_I4_S, (short)idx++);            // 3  {temp, idx}

                    for (int k = 0; k < 8; k++)
                    {
                        il.Emit(OpCodes.Ldarg_0);                       // {in} 
                        il.Emit(OpCodes.Ldc_I4_S, (short)(i * 8 + k));  // {in,idx}
                        il.Emit(OpCodes.Ldelem_R4);                     // {in[idx]}
                        il.Emit(OpCodes.Ldc_R4, c[k, j]);               // {in[idx],c[k,j]}
                        il.Emit(OpCodes.Mul);                           // {in[idx]*c[k,j]}
                        if (k != 0) il.Emit(OpCodes.Add);
                    }

                    il.Emit(OpCodes.Stelem_R4);                         // {}
                }
            }

            var meth = typeof(DCT).GetMethod("SetValueClipped",
                BindingFlags.Static | BindingFlags.Public, null,
                CallingConventions.Standard,
                new Type[] { 
                    typeof(byte[,]),    // arr
                    typeof(int),        // i
                    typeof(int),        // j
                    typeof(float) }     // val
                , null);

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    il.Emit(OpCodes.Ldarg_2);               //   {output}
                    il.Emit(OpCodes.Ldc_I4_S, (short)i);    //   {output,i}
                    il.Emit(OpCodes.Ldc_I4_S, (short)j);    // X={output,i,j}

                    il.Emit(OpCodes.Ldc_R4, 128.0f);        // {X,128.0f}

                    for (int k = 0; k < 8; k++)
                    {
                        il.Emit(OpCodes.Ldarg_1);           // {X,temp} 
                        il.Emit(OpCodes.Ldc_I4_S,
                            (short)(k * 8 + j));            // {X,temp,idx}
                        il.Emit(OpCodes.Ldelem_R4);         // {X,temp[idx]}
                        il.Emit(OpCodes.Ldc_R4, cT[i, k]);  // {X,temp[idx],cT[i,k]}
                        il.Emit(OpCodes.Mul);               // {X,in[idx]*c[k,j]}
                        il.Emit(OpCodes.Add);
                    }

                    il.EmitCall(OpCodes.Call, meth, null);
                }
            }

            il.Emit(OpCodes.Ret);

            return (IDCTFunc)idctMethod.CreateDelegate(typeof(IDCTFunc));
        }

        private delegate void IDCTFunc(float[] input, float[] temp, byte[,] output);
        private static IDCTFunc dynamicIDCT = null;
#endif


    }




}
