namespace Adamantium.Engine.NoiseGenerator
{
   class OpenSimplexHLSLpreparation
   {
      const float STRETCH_CONSTANT_2D = -0.211324865405187f;    //(1/Math.sqrt(2+1)-1)/2;
      const float SQUISH_CONSTANT_2D = 0.366025403784439f;      //(Math.sqrt(2+1)-1)/2;
      const float STRETCH_CONSTANT_3D = -1.0f / 6;              //(1/Math.sqrt(3+1)-1)/3;
      const float SQUISH_CONSTANT_3D = 1.0f / 3;                //(Math.sqrt(3+1)-1)/3;
      const float STRETCH_CONSTANT_4D = -0.138196601125011f;    //(1/Math.sqrt(4+1)-1)/4;
      const float SQUISH_CONSTANT_4D = 0.309016994374947f;      //(Math.sqrt(4+1)-1)/4;

      const float NORM_CONSTANT_2D = 47;
      const float NORM_CONSTANT_3D = 103;
      const float NORM_CONSTANT_4D = 30;

      const long DEFAULT_SEED = 0;

      public int[] perm;
      public int[] permGradIndex3D;

      //Initializes the class using a permutation array generated from a 64-bit seed.
      //Generates a proper permutation (i.e. doesn't merely perform N successive pair swaps on a base array)
      //Uses a simple 64-bit LCG.
      public OpenSimplexHLSLpreparation(long seed)
      {
         perm = new int[256];
         permGradIndex3D = new int[256];
         short[] source = new short[256];
         for (short i = 0; i < 256; i++)
            source[i] = i;
         seed = seed * 6364136223846793005L + 1442695040888963407L;
         seed = seed * 6364136223846793005L + 1442695040888963407L;
         seed = seed * 6364136223846793005L + 1442695040888963407L;
         for (int i = 255; i >= 0; i--)
         {
            seed = seed * 6364136223846793005L + 1442695040888963407L;
            int r = (int)((seed + 31) % (i + 1));
            if (r < 0)
               r += (i + 1);
            perm[i] = source[r];
            permGradIndex3D[i] = (short)((perm[i] % (Gradients3D.Length / 3)) * 3);
            source[r] = source[i];
         }
      }


      //2D OpenSimplex Noise.
      public float eval(float x, float y)
      {
         //Place input coordinates onto grid.
         float stretchOffset = (x + y) * STRETCH_CONSTANT_2D;
         float xs = x + stretchOffset;
         float ys = y + stretchOffset;

         //Floor to get grid coordinates of rhombus (stretched square) super-cell origin.
         int xsb = FastFloor(xs);
         int ysb = FastFloor(ys);

         //Skew out to get actual coordinates of rhombus origin. We'll need these later.
         float squishOffset = (xsb + ysb) * SQUISH_CONSTANT_2D;
         float xb = xsb + squishOffset;
         float yb = ysb + squishOffset;

         //Compute grid coordinates relative to rhombus origin.
         float xins = xs - xsb;
         float yins = ys - ysb;

         //Sum those together to get a value that determines which region we're in.
         float inSum = xins + yins;

         //Positions relative to origin point.
         float dx0 = x - xb;
         float dy0 = y - yb;

         //We'll be defining these inside the next block and using them afterwards.
         float dx_ext, dy_ext;
         int xsv_ext, ysv_ext;

         float value = 0;

         //Contribution (1,0)
         float dx1 = dx0 - 1 - SQUISH_CONSTANT_2D;
         float dy1 = dy0 - 0 - SQUISH_CONSTANT_2D;
         float attn1 = 2 - dx1 * dx1 - dy1 * dy1;
         if (attn1 > 0)
         {
            attn1 *= attn1;
            value += attn1 * attn1 * Extrapolate(xsb + 1, ysb + 0, dx1, dy1);
         }

         //Contribution (0,1)
         float dx2 = dx0 - 0 - SQUISH_CONSTANT_2D;
         float dy2 = dy0 - 1 - SQUISH_CONSTANT_2D;
         float attn2 = 2 - dx2 * dx2 - dy2 * dy2;
         if (attn2 > 0)
         {
            attn2 *= attn2;
            value += attn2 * attn2 * Extrapolate(xsb + 0, ysb + 1, dx2, dy2);
         }

         if (inSum <= 1)
         { //We're inside the triangle (2-Simplex) at (0,0)
            float zins = 1 - inSum;
            if (zins > xins || zins > yins)
            { //(0,0) is one of the closest two triangular vertices
               if (xins > yins)
               {
                  xsv_ext = xsb + 1;
                  ysv_ext = ysb - 1;
                  dx_ext = dx0 - 1;
                  dy_ext = dy0 + 1;
               }
               else
               {
                  xsv_ext = xsb - 1;
                  ysv_ext = ysb + 1;
                  dx_ext = dx0 + 1;
                  dy_ext = dy0 - 1;
               }
            }
            else
            { //(1,0) and (0,1) are the closest two vertices.
               xsv_ext = xsb + 1;
               ysv_ext = ysb + 1;
               dx_ext = dx0 - 1 - 2 * SQUISH_CONSTANT_2D;
               dy_ext = dy0 - 1 - 2 * SQUISH_CONSTANT_2D;
            }
         }
         else
         { //We're inside the triangle (2-Simplex) at (1,1)
            float zins = 2 - inSum;
            if (zins < xins || zins < yins)
            { //(0,0) is one of the closest two triangular vertices
               if (xins > yins)
               {
                  xsv_ext = xsb + 2;
                  ysv_ext = ysb + 0;
                  dx_ext = dx0 - 2 - 2 * SQUISH_CONSTANT_2D;
                  dy_ext = dy0 + 0 - 2 * SQUISH_CONSTANT_2D;
               }
               else
               {
                  xsv_ext = xsb + 0;
                  ysv_ext = ysb + 2;
                  dx_ext = dx0 + 0 - 2 * SQUISH_CONSTANT_2D;
                  dy_ext = dy0 - 2 - 2 * SQUISH_CONSTANT_2D;
               }
            }
            else
            { //(1,0) and (0,1) are the closest two vertices.
               dx_ext = dx0;
               dy_ext = dy0;
               xsv_ext = xsb;
               ysv_ext = ysb;
            }
            xsb += 1;
            ysb += 1;
            dx0 = dx0 - 1 - 2 * SQUISH_CONSTANT_2D;
            dy0 = dy0 - 1 - 2 * SQUISH_CONSTANT_2D;
         }

         //Contribution (0,0) or (1,1)
         float attn0 = 2 - dx0 * dx0 - dy0 * dy0;
         if (attn0 > 0)
         {
            attn0 *= attn0;
            value += attn0 * attn0 * Extrapolate(xsb, ysb, dx0, dy0);
         }

         //Extra Vertex
         float attn_ext = 2 - dx_ext * dx_ext - dy_ext * dy_ext;
         if (attn_ext > 0)
         {
            attn_ext *= attn_ext;
            value += attn_ext * attn_ext * Extrapolate(xsv_ext, ysv_ext, dx_ext, dy_ext);
         }

         return value / NORM_CONSTANT_2D;
      }

      //3D OpenSimplex Noise.
      public float eval(float x, float y, float z)
      {
         //Place input coordinates on simplectic honeycomb.
         float stretchOffset = (x + y + z) * STRETCH_CONSTANT_3D;
         float xs = x + stretchOffset;
         float ys = y + stretchOffset;
         float zs = z + stretchOffset;

         //Floor to get simplectic honeycomb coordinates of rhombohedron (stretched cube) super-cell origin.
         int xsb = FastFloor(xs);
         int ysb = FastFloor(ys);
         int zsb = FastFloor(zs);

         //Skew out to get actual coordinates of rhombohedron origin. We'll need these later.
         float squishOffset = (xsb + ysb + zsb) * SQUISH_CONSTANT_3D;
         float xb = xsb + squishOffset;
         float yb = ysb + squishOffset;
         float zb = zsb + squishOffset;

         //Compute simplectic honeycomb coordinates relative to rhombohedral origin.
         float xins = xs - xsb;
         float yins = ys - ysb;
         float zins = zs - zsb;

         //Sum those together to get a value that determines which region we're in.
         float inSum = xins + yins + zins;

         //Positions relative to origin point.
         float dx0 = x - xb;
         float dy0 = y - yb;
         float dz0 = z - zb;

         //We'll be defining these inside the next block and using them afterwards.
         float dx_ext0, dy_ext0, dz_ext0;
         float dx_ext1, dy_ext1, dz_ext1;
         int xsv_ext0, ysv_ext0, zsv_ext0;
         int xsv_ext1, ysv_ext1, zsv_ext1;

         float value = 0;
         if (inSum <= 1)
         { //We're inside the tetrahedron (3-Simplex) at (0,0,0)

            //Determine which two of (0,0,1), (0,1,0), (1,0,0) are closest.
            int aPoint = 0x01;
            float aScore = xins;
            int bPoint = 0x02;
            float bScore = yins;
            if (aScore >= bScore && zins > bScore)
            {
               bScore = zins;
               bPoint = 0x04;
            }
            else if (aScore < bScore && zins > aScore)
            {
               aScore = zins;
               aPoint = 0x04;
            }

            //Now we determine the two lattice points not part of the tetrahedron that may contribute.
            //This depends on the closest two tetrahedral vertices, including (0,0,0)
            float wins = 1 - inSum;
            if (wins > aScore || wins > bScore)
            { //(0,0,0) is one of the closest two tetrahedral vertices.
               int c = (bScore > aScore ? bPoint : aPoint); //Our other closest vertex is the closest out of a and b.

               if ((c & 0x01) == 0)
               {
                  xsv_ext0 = xsb - 1;
                  xsv_ext1 = xsb;
                  dx_ext0 = dx0 + 1;
                  dx_ext1 = dx0;
               }
               else
               {
                  xsv_ext0 = xsv_ext1 = xsb + 1;
                  dx_ext0 = dx_ext1 = dx0 - 1;
               }

               if ((c & 0x02) == 0)
               {
                  ysv_ext0 = ysv_ext1 = ysb;
                  dy_ext0 = dy_ext1 = dy0;
                  if ((c & 0x01) == 0)
                  {
                     ysv_ext1 -= 1;
                     dy_ext1 += 1;
                  }
                  else
                  {
                     ysv_ext0 -= 1;
                     dy_ext0 += 1;
                  }
               }
               else
               {
                  ysv_ext0 = ysv_ext1 = ysb + 1;
                  dy_ext0 = dy_ext1 = dy0 - 1;
               }

               if ((c & 0x04) == 0)
               {
                  zsv_ext0 = zsb;
                  zsv_ext1 = zsb - 1;
                  dz_ext0 = dz0;
                  dz_ext1 = dz0 + 1;
               }
               else
               {
                  zsv_ext0 = zsv_ext1 = zsb + 1;
                  dz_ext0 = dz_ext1 = dz0 - 1;
               }
            }
            else
            { //(0,0,0) is not one of the closest two tetrahedral vertices.
               int c = (int)(aPoint | bPoint); //Our two extra vertices are determined by the closest two.

               if ((c & 0x01) == 0)
               {
                  xsv_ext0 = xsb;
                  xsv_ext1 = xsb - 1;
                  dx_ext0 = dx0 - 2 * SQUISH_CONSTANT_3D;
                  dx_ext1 = dx0 + 1 - SQUISH_CONSTANT_3D;
               }
               else
               {
                  xsv_ext0 = xsv_ext1 = xsb + 1;
                  dx_ext0 = dx0 - 1 - 2 * SQUISH_CONSTANT_3D;
                  dx_ext1 = dx0 - 1 - SQUISH_CONSTANT_3D;
               }

               if ((c & 0x02) == 0)
               {
                  ysv_ext0 = ysb;
                  ysv_ext1 = ysb - 1;
                  dy_ext0 = dy0 - 2 * SQUISH_CONSTANT_3D;
                  dy_ext1 = dy0 + 1 - SQUISH_CONSTANT_3D;
               }
               else
               {
                  ysv_ext0 = ysv_ext1 = ysb + 1;
                  dy_ext0 = dy0 - 1 - 2 * SQUISH_CONSTANT_3D;
                  dy_ext1 = dy0 - 1 - SQUISH_CONSTANT_3D;
               }

               if ((c & 0x04) == 0)
               {
                  zsv_ext0 = zsb;
                  zsv_ext1 = zsb - 1;
                  dz_ext0 = dz0 - 2 * SQUISH_CONSTANT_3D;
                  dz_ext1 = dz0 + 1 - SQUISH_CONSTANT_3D;
               }
               else
               {
                  zsv_ext0 = zsv_ext1 = zsb + 1;
                  dz_ext0 = dz0 - 1 - 2 * SQUISH_CONSTANT_3D;
                  dz_ext1 = dz0 - 1 - SQUISH_CONSTANT_3D;
               }
            }

            //Contribution (0,0,0)
            float attn0 = 2 - dx0 * dx0 - dy0 * dy0 - dz0 * dz0;
            if (attn0 > 0)
            {
               attn0 *= attn0;
               value += attn0 * attn0 * Extrapolate(xsb + 0, ysb + 0, zsb + 0, dx0, dy0, dz0);
            }

            //Contribution (1,0,0)
            float dx1 = dx0 - 1 - SQUISH_CONSTANT_3D;
            float dy1 = dy0 - 0 - SQUISH_CONSTANT_3D;
            float dz1 = dz0 - 0 - SQUISH_CONSTANT_3D;
            float attn1 = 2 - dx1 * dx1 - dy1 * dy1 - dz1 * dz1;
            if (attn1 > 0)
            {
               attn1 *= attn1;
               value += attn1 * attn1 * Extrapolate(xsb + 1, ysb + 0, zsb + 0, dx1, dy1, dz1);
            }

            //Contribution (0,1,0)
            float dx2 = dx0 - 0 - SQUISH_CONSTANT_3D;
            float dy2 = dy0 - 1 - SQUISH_CONSTANT_3D;
            float dz2 = dz1;
            float attn2 = 2 - dx2 * dx2 - dy2 * dy2 - dz2 * dz2;
            if (attn2 > 0)
            {
               attn2 *= attn2;
               value += attn2 * attn2 * Extrapolate(xsb + 0, ysb + 1, zsb + 0, dx2, dy2, dz2);
            }

            //Contribution (0,0,1)
            float dx3 = dx2;
            float dy3 = dy1;
            float dz3 = dz0 - 1 - SQUISH_CONSTANT_3D;
            float attn3 = 2 - dx3 * dx3 - dy3 * dy3 - dz3 * dz3;
            if (attn3 > 0)
            {
               attn3 *= attn3;
               value += attn3 * attn3 * Extrapolate(xsb + 0, ysb + 0, zsb + 1, dx3, dy3, dz3);
            }
         }
         else if (inSum >= 2)
         { //We're inside the tetrahedron (3-Simplex) at (1,1,1)

            //Determine which two tetrahedral vertices are the closest, out of (1,1,0), (1,0,1), (0,1,1) but not (1,1,1).
            int aPoint = 0x06;
            float aScore = xins;
            int bPoint = 0x05;
            float bScore = yins;
            if (aScore <= bScore && zins < bScore)
            {
               bScore = zins;
               bPoint = 0x03;
            }
            else if (aScore > bScore && zins < aScore)
            {
               aScore = zins;
               aPoint = 0x03;
            }

            //Now we determine the two lattice points not part of the tetrahedron that may contribute.
            //This depends on the closest two tetrahedral vertices, including (1,1,1)
            float wins = 3 - inSum;
            if (wins < aScore || wins < bScore)
            { //(1,1,1) is one of the closest two tetrahedral vertices.
               int c = (bScore < aScore ? bPoint : aPoint); //Our other closest vertex is the closest out of a and b.

               if ((c & 0x01) != 0)
               {
                  xsv_ext0 = xsb + 2;
                  xsv_ext1 = xsb + 1;
                  dx_ext0 = dx0 - 2 - 3 * SQUISH_CONSTANT_3D;
                  dx_ext1 = dx0 - 1 - 3 * SQUISH_CONSTANT_3D;
               }
               else
               {
                  xsv_ext0 = xsv_ext1 = xsb;
                  dx_ext0 = dx_ext1 = dx0 - 3 * SQUISH_CONSTANT_3D;
               }

               if ((c & 0x02) != 0)
               {
                  ysv_ext0 = ysv_ext1 = ysb + 1;
                  dy_ext0 = dy_ext1 = dy0 - 1 - 3 * SQUISH_CONSTANT_3D;
                  if ((c & 0x01) != 0)
                  {
                     ysv_ext1 += 1;
                     dy_ext1 -= 1;
                  }
                  else
                  {
                     ysv_ext0 += 1;
                     dy_ext0 -= 1;
                  }
               }
               else
               {
                  ysv_ext0 = ysv_ext1 = ysb;
                  dy_ext0 = dy_ext1 = dy0 - 3 * SQUISH_CONSTANT_3D;
               }

               if ((c & 0x04) != 0)
               {
                  zsv_ext0 = zsb + 1;
                  zsv_ext1 = zsb + 2;
                  dz_ext0 = dz0 - 1 - 3 * SQUISH_CONSTANT_3D;
                  dz_ext1 = dz0 - 2 - 3 * SQUISH_CONSTANT_3D;
               }
               else
               {
                  zsv_ext0 = zsv_ext1 = zsb;
                  dz_ext0 = dz_ext1 = dz0 - 3 * SQUISH_CONSTANT_3D;
               }
            }
            else
            { //(1,1,1) is not one of the closest two tetrahedral vertices.
               int c = (int)(aPoint & bPoint); //Our two extra vertices are determined by the closest two.

               if ((c & 0x01) != 0)
               {
                  xsv_ext0 = xsb + 1;
                  xsv_ext1 = xsb + 2;
                  dx_ext0 = dx0 - 1 - SQUISH_CONSTANT_3D;
                  dx_ext1 = dx0 - 2 - 2 * SQUISH_CONSTANT_3D;
               }
               else
               {
                  xsv_ext0 = xsv_ext1 = xsb;
                  dx_ext0 = dx0 - SQUISH_CONSTANT_3D;
                  dx_ext1 = dx0 - 2 * SQUISH_CONSTANT_3D;
               }

               if ((c & 0x02) != 0)
               {
                  ysv_ext0 = ysb + 1;
                  ysv_ext1 = ysb + 2;
                  dy_ext0 = dy0 - 1 - SQUISH_CONSTANT_3D;
                  dy_ext1 = dy0 - 2 - 2 * SQUISH_CONSTANT_3D;
               }
               else
               {
                  ysv_ext0 = ysv_ext1 = ysb;
                  dy_ext0 = dy0 - SQUISH_CONSTANT_3D;
                  dy_ext1 = dy0 - 2 * SQUISH_CONSTANT_3D;
               }

               if ((c & 0x04) != 0)
               {
                  zsv_ext0 = zsb + 1;
                  zsv_ext1 = zsb + 2;
                  dz_ext0 = dz0 - 1 - SQUISH_CONSTANT_3D;
                  dz_ext1 = dz0 - 2 - 2 * SQUISH_CONSTANT_3D;
               }
               else
               {
                  zsv_ext0 = zsv_ext1 = zsb;
                  dz_ext0 = dz0 - SQUISH_CONSTANT_3D;
                  dz_ext1 = dz0 - 2 * SQUISH_CONSTANT_3D;
               }
            }

            //Contribution (1,1,0)
            float dx3 = dx0 - 1 - 2 * SQUISH_CONSTANT_3D;
            float dy3 = dy0 - 1 - 2 * SQUISH_CONSTANT_3D;
            float dz3 = dz0 - 0 - 2 * SQUISH_CONSTANT_3D;
            float attn3 = 2 - dx3 * dx3 - dy3 * dy3 - dz3 * dz3;
            if (attn3 > 0)
            {
               attn3 *= attn3;
               value += attn3 * attn3 * Extrapolate(xsb + 1, ysb + 1, zsb + 0, dx3, dy3, dz3);
            }

            //Contribution (1,0,1)
            float dx2 = dx3;
            float dy2 = dy0 - 0 - 2 * SQUISH_CONSTANT_3D;
            float dz2 = dz0 - 1 - 2 * SQUISH_CONSTANT_3D;
            float attn2 = 2 - dx2 * dx2 - dy2 * dy2 - dz2 * dz2;
            if (attn2 > 0)
            {
               attn2 *= attn2;
               value += attn2 * attn2 * Extrapolate(xsb + 1, ysb + 0, zsb + 1, dx2, dy2, dz2);
            }

            //Contribution (0,1,1)
            float dx1 = dx0 - 0 - 2 * SQUISH_CONSTANT_3D;
            float dy1 = dy3;
            float dz1 = dz2;
            float attn1 = 2 - dx1 * dx1 - dy1 * dy1 - dz1 * dz1;
            if (attn1 > 0)
            {
               attn1 *= attn1;
               value += attn1 * attn1 * Extrapolate(xsb + 0, ysb + 1, zsb + 1, dx1, dy1, dz1);
            }

            //Contribution (1,1,1)
            dx0 = dx0 - 1 - 3 * SQUISH_CONSTANT_3D;
            dy0 = dy0 - 1 - 3 * SQUISH_CONSTANT_3D;
            dz0 = dz0 - 1 - 3 * SQUISH_CONSTANT_3D;
            float attn0 = 2 - dx0 * dx0 - dy0 * dy0 - dz0 * dz0;
            if (attn0 > 0)
            {
               attn0 *= attn0;
               value += attn0 * attn0 * Extrapolate(xsb + 1, ysb + 1, zsb + 1, dx0, dy0, dz0);
            }
         }
         else
         { //We're inside the octahedron (Rectified 3-Simplex) in between.
            float aScore;
            int aPoint;
            bool aIsFurtherSide;
            float bScore;
            int bPoint;
            bool bIsFurtherSide;

            //Decide between point (0,0,1) and (1,1,0) as closest
            float p1 = xins + yins;
            if (p1 > 1)
            {
               aScore = p1 - 1;
               aPoint = 0x03;
               aIsFurtherSide = true;
            }
            else
            {
               aScore = 1 - p1;
               aPoint = 0x04;
               aIsFurtherSide = false;
            }

            //Decide between point (0,1,0) and (1,0,1) as closest
            float p2 = xins + zins;
            if (p2 > 1)
            {
               bScore = p2 - 1;
               bPoint = 0x05;
               bIsFurtherSide = true;
            }
            else
            {
               bScore = 1 - p2;
               bPoint = 0x02;
               bIsFurtherSide = false;
            }

            //The closest out of the two (1,0,0) and (0,1,1) will replace the furthest out of the two decided above, if closer.
            float p3 = yins + zins;
            if (p3 > 1)
            {
               float score = p3 - 1;
               if (aScore <= bScore && aScore < score)
               {
                  aScore = score;
                  aPoint = 0x06;
                  aIsFurtherSide = true;
               }
               else if (aScore > bScore && bScore < score)
               {
                  bScore = score;
                  bPoint = 0x06;
                  bIsFurtherSide = true;
               }
            }
            else
            {
               float score = 1 - p3;
               if (aScore <= bScore && aScore < score)
               {
                  aScore = score;
                  aPoint = 0x01;
                  aIsFurtherSide = false;
               }
               else if (aScore > bScore && bScore < score)
               {
                  bScore = score;
                  bPoint = 0x01;
                  bIsFurtherSide = false;
               }
            }

            //Where each of the two closest points are determines how the extra two vertices are calculated.
            if (aIsFurtherSide == bIsFurtherSide)
            {
               if (aIsFurtherSide)
               { //Both closest points on (1,1,1) side

                  //One of the two extra points is (1,1,1)
                  dx_ext0 = dx0 - 1 - 3 * SQUISH_CONSTANT_3D;
                  dy_ext0 = dy0 - 1 - 3 * SQUISH_CONSTANT_3D;
                  dz_ext0 = dz0 - 1 - 3 * SQUISH_CONSTANT_3D;
                  xsv_ext0 = xsb + 1;
                  ysv_ext0 = ysb + 1;
                  zsv_ext0 = zsb + 1;

                  //Other extra point is based on the shared axis.
                  int c = (int)(aPoint & bPoint);
                  if ((c & 0x01) != 0)
                  {
                     dx_ext1 = dx0 - 2 - 2 * SQUISH_CONSTANT_3D;
                     dy_ext1 = dy0 - 2 * SQUISH_CONSTANT_3D;
                     dz_ext1 = dz0 - 2 * SQUISH_CONSTANT_3D;
                     xsv_ext1 = xsb + 2;
                     ysv_ext1 = ysb;
                     zsv_ext1 = zsb;
                  }
                  else if ((c & 0x02) != 0)
                  {
                     dx_ext1 = dx0 - 2 * SQUISH_CONSTANT_3D;
                     dy_ext1 = dy0 - 2 - 2 * SQUISH_CONSTANT_3D;
                     dz_ext1 = dz0 - 2 * SQUISH_CONSTANT_3D;
                     xsv_ext1 = xsb;
                     ysv_ext1 = ysb + 2;
                     zsv_ext1 = zsb;
                  }
                  else
                  {
                     dx_ext1 = dx0 - 2 * SQUISH_CONSTANT_3D;
                     dy_ext1 = dy0 - 2 * SQUISH_CONSTANT_3D;
                     dz_ext1 = dz0 - 2 - 2 * SQUISH_CONSTANT_3D;
                     xsv_ext1 = xsb;
                     ysv_ext1 = ysb;
                     zsv_ext1 = zsb + 2;
                  }
               }
               else
               {//Both closest points on (0,0,0) side

                  //One of the two extra points is (0,0,0)
                  dx_ext0 = dx0;
                  dy_ext0 = dy0;
                  dz_ext0 = dz0;
                  xsv_ext0 = xsb;
                  ysv_ext0 = ysb;
                  zsv_ext0 = zsb;

                  //Other extra point is based on the omitted axis.
                  int c = (int)(aPoint | bPoint);
                  if ((c & 0x01) == 0)
                  {
                     dx_ext1 = dx0 + 1 - SQUISH_CONSTANT_3D;
                     dy_ext1 = dy0 - 1 - SQUISH_CONSTANT_3D;
                     dz_ext1 = dz0 - 1 - SQUISH_CONSTANT_3D;
                     xsv_ext1 = xsb - 1;
                     ysv_ext1 = ysb + 1;
                     zsv_ext1 = zsb + 1;
                  }
                  else if ((c & 0x02) == 0)
                  {
                     dx_ext1 = dx0 - 1 - SQUISH_CONSTANT_3D;
                     dy_ext1 = dy0 + 1 - SQUISH_CONSTANT_3D;
                     dz_ext1 = dz0 - 1 - SQUISH_CONSTANT_3D;
                     xsv_ext1 = xsb + 1;
                     ysv_ext1 = ysb - 1;
                     zsv_ext1 = zsb + 1;
                  }
                  else
                  {
                     dx_ext1 = dx0 - 1 - SQUISH_CONSTANT_3D;
                     dy_ext1 = dy0 - 1 - SQUISH_CONSTANT_3D;
                     dz_ext1 = dz0 + 1 - SQUISH_CONSTANT_3D;
                     xsv_ext1 = xsb + 1;
                     ysv_ext1 = ysb + 1;
                     zsv_ext1 = zsb - 1;
                  }
               }
            }
            else
            { //One point on (0,0,0) side, one point on (1,1,1) side
               int c1, c2;
               if (aIsFurtherSide)
               {
                  c1 = aPoint;
                  c2 = bPoint;
               }
               else
               {
                  c1 = bPoint;
                  c2 = aPoint;
               }

               //One contribution is a permutation of (1,1,-1)
               if ((c1 & 0x01) == 0)
               {
                  dx_ext0 = dx0 + 1 - SQUISH_CONSTANT_3D;
                  dy_ext0 = dy0 - 1 - SQUISH_CONSTANT_3D;
                  dz_ext0 = dz0 - 1 - SQUISH_CONSTANT_3D;
                  xsv_ext0 = xsb - 1;
                  ysv_ext0 = ysb + 1;
                  zsv_ext0 = zsb + 1;
               }
               else if ((c1 & 0x02) == 0)
               {
                  dx_ext0 = dx0 - 1 - SQUISH_CONSTANT_3D;
                  dy_ext0 = dy0 + 1 - SQUISH_CONSTANT_3D;
                  dz_ext0 = dz0 - 1 - SQUISH_CONSTANT_3D;
                  xsv_ext0 = xsb + 1;
                  ysv_ext0 = ysb - 1;
                  zsv_ext0 = zsb + 1;
               }
               else
               {
                  dx_ext0 = dx0 - 1 - SQUISH_CONSTANT_3D;
                  dy_ext0 = dy0 - 1 - SQUISH_CONSTANT_3D;
                  dz_ext0 = dz0 + 1 - SQUISH_CONSTANT_3D;
                  xsv_ext0 = xsb + 1;
                  ysv_ext0 = ysb + 1;
                  zsv_ext0 = zsb - 1;
               }

               //One contribution is a permutation of (0,0,2)
               dx_ext1 = dx0 - 2 * SQUISH_CONSTANT_3D;
               dy_ext1 = dy0 - 2 * SQUISH_CONSTANT_3D;
               dz_ext1 = dz0 - 2 * SQUISH_CONSTANT_3D;
               xsv_ext1 = xsb;
               ysv_ext1 = ysb;
               zsv_ext1 = zsb;
               if ((c2 & 0x01) != 0)
               {
                  dx_ext1 -= 2;
                  xsv_ext1 += 2;
               }
               else if ((c2 & 0x02) != 0)
               {
                  dy_ext1 -= 2;
                  ysv_ext1 += 2;
               }
               else
               {
                  dz_ext1 -= 2;
                  zsv_ext1 += 2;
               }
            }

            //Contribution (1,0,0)
            float dx1 = dx0 - 1 - SQUISH_CONSTANT_3D;
            float dy1 = dy0 - 0 - SQUISH_CONSTANT_3D;
            float dz1 = dz0 - 0 - SQUISH_CONSTANT_3D;
            float attn1 = 2 - dx1 * dx1 - dy1 * dy1 - dz1 * dz1;
            if (attn1 > 0)
            {
               attn1 *= attn1;
               value += attn1 * attn1 * Extrapolate(xsb + 1, ysb + 0, zsb + 0, dx1, dy1, dz1);
            }

            //Contribution (0,1,0)
            float dx2 = dx0 - 0 - SQUISH_CONSTANT_3D;
            float dy2 = dy0 - 1 - SQUISH_CONSTANT_3D;
            float dz2 = dz1;
            float attn2 = 2 - dx2 * dx2 - dy2 * dy2 - dz2 * dz2;
            if (attn2 > 0)
            {
               attn2 *= attn2;
               value += attn2 * attn2 * Extrapolate(xsb + 0, ysb + 1, zsb + 0, dx2, dy2, dz2);
            }

            //Contribution (0,0,1)
            float dx3 = dx2;
            float dy3 = dy1;
            float dz3 = dz0 - 1 - SQUISH_CONSTANT_3D;
            float attn3 = 2 - dx3 * dx3 - dy3 * dy3 - dz3 * dz3;
            if (attn3 > 0)
            {
               attn3 *= attn3;
               value += attn3 * attn3 * Extrapolate(xsb + 0, ysb + 0, zsb + 1, dx3, dy3, dz3);
            }

            //Contribution (1,1,0)
            float dx4 = dx0 - 1 - 2 * SQUISH_CONSTANT_3D;
            float dy4 = dy0 - 1 - 2 * SQUISH_CONSTANT_3D;
            float dz4 = dz0 - 0 - 2 * SQUISH_CONSTANT_3D;
            float attn4 = 2 - dx4 * dx4 - dy4 * dy4 - dz4 * dz4;
            if (attn4 > 0)
            {
               attn4 *= attn4;
               value += attn4 * attn4 * Extrapolate(xsb + 1, ysb + 1, zsb + 0, dx4, dy4, dz4);
            }

            //Contribution (1,0,1)
            float dx5 = dx4;
            float dy5 = dy0 - 0 - 2 * SQUISH_CONSTANT_3D;
            float dz5 = dz0 - 1 - 2 * SQUISH_CONSTANT_3D;
            float attn5 = 2 - dx5 * dx5 - dy5 * dy5 - dz5 * dz5;
            if (attn5 > 0)
            {
               attn5 *= attn5;
               value += attn5 * attn5 * Extrapolate(xsb + 1, ysb + 0, zsb + 1, dx5, dy5, dz5);
            }

            //Contribution (0,1,1)
            float dx6 = dx0 - 0 - 2 * SQUISH_CONSTANT_3D;
            float dy6 = dy4;
            float dz6 = dz5;
            float attn6 = 2 - dx6 * dx6 - dy6 * dy6 - dz6 * dz6;
            if (attn6 > 0)
            {
               attn6 *= attn6;
               value += attn6 * attn6 * Extrapolate(xsb + 0, ysb + 1, zsb + 1, dx6, dy6, dz6);
            }
         }

         //First extra vertex
         float attn_ext0 = 2 - dx_ext0 * dx_ext0 - dy_ext0 * dy_ext0 - dz_ext0 * dz_ext0;
         if (attn_ext0 > 0)
         {
            attn_ext0 *= attn_ext0;
            value += attn_ext0 * attn_ext0 * Extrapolate(xsv_ext0, ysv_ext0, zsv_ext0, dx_ext0, dy_ext0, dz_ext0);
         }

         //Second extra vertex
         float attn_ext1 = 2 - dx_ext1 * dx_ext1 - dy_ext1 * dy_ext1 - dz_ext1 * dz_ext1;
         if (attn_ext1 > 0)
         {
            attn_ext1 *= attn_ext1;
            value += attn_ext1 * attn_ext1 * Extrapolate(xsv_ext1, ysv_ext1, zsv_ext1, dx_ext1, dy_ext1, dz_ext1);
         }

         return value / NORM_CONSTANT_3D;
      }

      //4D OpenSimplex Noise.
      public float eval(float x, float y, float z, float w)
      {

         //Place input coordinates on simplectic honeycomb.
         float stretchOffset = (x + y + z + w) * STRETCH_CONSTANT_4D;
         float xs = x + stretchOffset;
         float ys = y + stretchOffset;
         float zs = z + stretchOffset;
         float ws = w + stretchOffset;

         //Floor to get simplectic honeycomb coordinates of rhombo-hypercube super-cell origin.
         int xsb = FastFloor(xs);
         int ysb = FastFloor(ys);
         int zsb = FastFloor(zs);
         int wsb = FastFloor(ws);

         //Skew out to get actual coordinates of stretched rhombo-hypercube origin. We'll need these later.
         float squishOffset = (xsb + ysb + zsb + wsb) * SQUISH_CONSTANT_4D;
         float xb = xsb + squishOffset;
         float yb = ysb + squishOffset;
         float zb = zsb + squishOffset;
         float wb = wsb + squishOffset;

         //Compute simplectic honeycomb coordinates relative to rhombo-hypercube origin.
         float xins = xs - xsb;
         float yins = ys - ysb;
         float zins = zs - zsb;
         float wins = ws - wsb;

         //Sum those together to get a value that determines which region we're in.
         float inSum = xins + yins + zins + wins;

         //Positions relative to origin point.
         float dx0 = x - xb;
         float dy0 = y - yb;
         float dz0 = z - zb;
         float dw0 = w - wb;

         //We'll be defining these inside the next block and using them afterwards.
         float dx_ext0, dy_ext0, dz_ext0, dw_ext0;
         float dx_ext1, dy_ext1, dz_ext1, dw_ext1;
         float dx_ext2, dy_ext2, dz_ext2, dw_ext2;
         int xsv_ext0, ysv_ext0, zsv_ext0, wsv_ext0;
         int xsv_ext1, ysv_ext1, zsv_ext1, wsv_ext1;
         int xsv_ext2, ysv_ext2, zsv_ext2, wsv_ext2;

         float value = 0;
         if (inSum <= 1)
         { //We're inside the pentachoron (4-Simplex) at (0,0,0,0)

            //Determine which two of (0,0,0,1), (0,0,1,0), (0,1,0,0), (1,0,0,0) are closest.
            int aPoint = 0x01;
            float aScore = xins;
            int bPoint = 0x02;
            float bScore = yins;
            if (aScore >= bScore && zins > bScore)
            {
               bScore = zins;
               bPoint = 0x04;
            }
            else if (aScore < bScore && zins > aScore)
            {
               aScore = zins;
               aPoint = 0x04;
            }
            if (aScore >= bScore && wins > bScore)
            {
               bScore = wins;
               bPoint = 0x08;
            }
            else if (aScore < bScore && wins > aScore)
            {
               aScore = wins;
               aPoint = 0x08;
            }

            //Now we determine the three lattice points not part of the pentachoron that may contribute.
            //This depends on the closest two pentachoron vertices, including (0,0,0,0)
            float uins = 1 - inSum;
            if (uins > aScore || uins > bScore)
            { //(0,0,0,0) is one of the closest two pentachoron vertices.
               int c = (bScore > aScore ? bPoint : aPoint); //Our other closest vertex is the closest out of a and b.
               if ((c & 0x01) == 0)
               {
                  xsv_ext0 = xsb - 1;
                  xsv_ext1 = xsv_ext2 = xsb;
                  dx_ext0 = dx0 + 1;
                  dx_ext1 = dx_ext2 = dx0;
               }
               else
               {
                  xsv_ext0 = xsv_ext1 = xsv_ext2 = xsb + 1;
                  dx_ext0 = dx_ext1 = dx_ext2 = dx0 - 1;
               }

               if ((c & 0x02) == 0)
               {
                  ysv_ext0 = ysv_ext1 = ysv_ext2 = ysb;
                  dy_ext0 = dy_ext1 = dy_ext2 = dy0;
                  if ((c & 0x01) == 0x01)
                  {
                     ysv_ext0 -= 1;
                     dy_ext0 += 1;
                  }
                  else
                  {
                     ysv_ext1 -= 1;
                     dy_ext1 += 1;
                  }
               }
               else
               {
                  ysv_ext0 = ysv_ext1 = ysv_ext2 = ysb + 1;
                  dy_ext0 = dy_ext1 = dy_ext2 = dy0 - 1;
               }

               if ((c & 0x04) == 0)
               {
                  zsv_ext0 = zsv_ext1 = zsv_ext2 = zsb;
                  dz_ext0 = dz_ext1 = dz_ext2 = dz0;
                  if ((c & 0x03) != 0)
                  {
                     if ((c & 0x03) == 0x03)
                     {
                        zsv_ext0 -= 1;
                        dz_ext0 += 1;
                     }
                     else
                     {
                        zsv_ext1 -= 1;
                        dz_ext1 += 1;
                     }
                  }
                  else
                  {
                     zsv_ext2 -= 1;
                     dz_ext2 += 1;
                  }
               }
               else
               {
                  zsv_ext0 = zsv_ext1 = zsv_ext2 = zsb + 1;
                  dz_ext0 = dz_ext1 = dz_ext2 = dz0 - 1;
               }

               if ((c & 0x08) == 0)
               {
                  wsv_ext0 = wsv_ext1 = wsb;
                  wsv_ext2 = wsb - 1;
                  dw_ext0 = dw_ext1 = dw0;
                  dw_ext2 = dw0 + 1;
               }
               else
               {
                  wsv_ext0 = wsv_ext1 = wsv_ext2 = wsb + 1;
                  dw_ext0 = dw_ext1 = dw_ext2 = dw0 - 1;
               }
            }
            else
            { //(0,0,0,0) is not one of the closest two pentachoron vertices.
               int c = (int)(aPoint | bPoint); //Our three extra vertices are determined by the closest two.

               if ((c & 0x01) == 0)
               {
                  xsv_ext0 = xsv_ext2 = xsb;
                  xsv_ext1 = xsb - 1;
                  dx_ext0 = dx0 - 2 * SQUISH_CONSTANT_4D;
                  dx_ext1 = dx0 + 1 - SQUISH_CONSTANT_4D;
                  dx_ext2 = dx0 - SQUISH_CONSTANT_4D;
               }
               else
               {
                  xsv_ext0 = xsv_ext1 = xsv_ext2 = xsb + 1;
                  dx_ext0 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dx_ext1 = dx_ext2 = dx0 - 1 - SQUISH_CONSTANT_4D;
               }

               if ((c & 0x02) == 0)
               {
                  ysv_ext0 = ysv_ext1 = ysv_ext2 = ysb;
                  dy_ext0 = dy0 - 2 * SQUISH_CONSTANT_4D;
                  dy_ext1 = dy_ext2 = dy0 - SQUISH_CONSTANT_4D;
                  if ((c & 0x01) == 0x01)
                  {
                     ysv_ext1 -= 1;
                     dy_ext1 += 1;
                  }
                  else
                  {
                     ysv_ext2 -= 1;
                     dy_ext2 += 1;
                  }
               }
               else
               {
                  ysv_ext0 = ysv_ext1 = ysv_ext2 = ysb + 1;
                  dy_ext0 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dy_ext1 = dy_ext2 = dy0 - 1 - SQUISH_CONSTANT_4D;
               }

               if ((c & 0x04) == 0)
               {
                  zsv_ext0 = zsv_ext1 = zsv_ext2 = zsb;
                  dz_ext0 = dz0 - 2 * SQUISH_CONSTANT_4D;
                  dz_ext1 = dz_ext2 = dz0 - SQUISH_CONSTANT_4D;
                  if ((c & 0x03) == 0x03)
                  {
                     zsv_ext1 -= 1;
                     dz_ext1 += 1;
                  }
                  else
                  {
                     zsv_ext2 -= 1;
                     dz_ext2 += 1;
                  }
               }
               else
               {
                  zsv_ext0 = zsv_ext1 = zsv_ext2 = zsb + 1;
                  dz_ext0 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dz_ext1 = dz_ext2 = dz0 - 1 - SQUISH_CONSTANT_4D;
               }

               if ((c & 0x08) == 0)
               {
                  wsv_ext0 = wsv_ext1 = wsb;
                  wsv_ext2 = wsb - 1;
                  dw_ext0 = dw0 - 2 * SQUISH_CONSTANT_4D;
                  dw_ext1 = dw0 - SQUISH_CONSTANT_4D;
                  dw_ext2 = dw0 + 1 - SQUISH_CONSTANT_4D;
               }
               else
               {
                  wsv_ext0 = wsv_ext1 = wsv_ext2 = wsb + 1;
                  dw_ext0 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dw_ext1 = dw_ext2 = dw0 - 1 - SQUISH_CONSTANT_4D;
               }
            }

            //Contribution (0,0,0,0)
            float attn0 = 2 - dx0 * dx0 - dy0 * dy0 - dz0 * dz0 - dw0 * dw0;
            if (attn0 > 0)
            {
               attn0 *= attn0;
               value += attn0 * attn0 * Extrapolate(xsb + 0, ysb + 0, zsb + 0, wsb + 0, dx0, dy0, dz0, dw0);
            }

            //Contribution (1,0,0,0)
            float dx1 = dx0 - 1 - SQUISH_CONSTANT_4D;
            float dy1 = dy0 - 0 - SQUISH_CONSTANT_4D;
            float dz1 = dz0 - 0 - SQUISH_CONSTANT_4D;
            float dw1 = dw0 - 0 - SQUISH_CONSTANT_4D;
            float attn1 = 2 - dx1 * dx1 - dy1 * dy1 - dz1 * dz1 - dw1 * dw1;
            if (attn1 > 0)
            {
               attn1 *= attn1;
               value += attn1 * attn1 * Extrapolate(xsb + 1, ysb + 0, zsb + 0, wsb + 0, dx1, dy1, dz1, dw1);
            }

            //Contribution (0,1,0,0)
            float dx2 = dx0 - 0 - SQUISH_CONSTANT_4D;
            float dy2 = dy0 - 1 - SQUISH_CONSTANT_4D;
            float dz2 = dz1;
            float dw2 = dw1;
            float attn2 = 2 - dx2 * dx2 - dy2 * dy2 - dz2 * dz2 - dw2 * dw2;
            if (attn2 > 0)
            {
               attn2 *= attn2;
               value += attn2 * attn2 * Extrapolate(xsb + 0, ysb + 1, zsb + 0, wsb + 0, dx2, dy2, dz2, dw2);
            }

            //Contribution (0,0,1,0)
            float dx3 = dx2;
            float dy3 = dy1;
            float dz3 = dz0 - 1 - SQUISH_CONSTANT_4D;
            float dw3 = dw1;
            float attn3 = 2 - dx3 * dx3 - dy3 * dy3 - dz3 * dz3 - dw3 * dw3;
            if (attn3 > 0)
            {
               attn3 *= attn3;
               value += attn3 * attn3 * Extrapolate(xsb + 0, ysb + 0, zsb + 1, wsb + 0, dx3, dy3, dz3, dw3);
            }

            //Contribution (0,0,0,1)
            float dx4 = dx2;
            float dy4 = dy1;
            float dz4 = dz1;
            float dw4 = dw0 - 1 - SQUISH_CONSTANT_4D;
            float attn4 = 2 - dx4 * dx4 - dy4 * dy4 - dz4 * dz4 - dw4 * dw4;
            if (attn4 > 0)
            {
               attn4 *= attn4;
               value += attn4 * attn4 * Extrapolate(xsb + 0, ysb + 0, zsb + 0, wsb + 1, dx4, dy4, dz4, dw4);
            }
         }
         else if (inSum >= 3)
         { //We're inside the pentachoron (4-Simplex) at (1,1,1,1)
           //Determine which two of (1,1,1,0), (1,1,0,1), (1,0,1,1), (0,1,1,1) are closest.
            int aPoint = 0x0E;
            float aScore = xins;
            int bPoint = 0x0D;
            float bScore = yins;
            if (aScore <= bScore && zins < bScore)
            {
               bScore = zins;
               bPoint = 0x0B;
            }
            else if (aScore > bScore && zins < aScore)
            {
               aScore = zins;
               aPoint = 0x0B;
            }
            if (aScore <= bScore && wins < bScore)
            {
               bScore = wins;
               bPoint = 0x07;
            }
            else if (aScore > bScore && wins < aScore)
            {
               aScore = wins;
               aPoint = 0x07;
            }

            //Now we determine the three lattice points not part of the pentachoron that may contribute.
            //This depends on the closest two pentachoron vertices, including (0,0,0,0)
            float uins = 4 - inSum;
            if (uins < aScore || uins < bScore)
            { //(1,1,1,1) is one of the closest two pentachoron vertices.
               int c = (bScore < aScore ? bPoint : aPoint); //Our other closest vertex is the closest out of a and b.

               if ((c & 0x01) != 0)
               {
                  xsv_ext0 = xsb + 2;
                  xsv_ext1 = xsv_ext2 = xsb + 1;
                  dx_ext0 = dx0 - 2 - 4 * SQUISH_CONSTANT_4D;
                  dx_ext1 = dx_ext2 = dx0 - 1 - 4 * SQUISH_CONSTANT_4D;
               }
               else
               {
                  xsv_ext0 = xsv_ext1 = xsv_ext2 = xsb;
                  dx_ext0 = dx_ext1 = dx_ext2 = dx0 - 4 * SQUISH_CONSTANT_4D;
               }

               if ((c & 0x02) != 0)
               {
                  ysv_ext0 = ysv_ext1 = ysv_ext2 = ysb + 1;
                  dy_ext0 = dy_ext1 = dy_ext2 = dy0 - 1 - 4 * SQUISH_CONSTANT_4D;
                  if ((c & 0x01) != 0)
                  {
                     ysv_ext1 += 1;
                     dy_ext1 -= 1;
                  }
                  else
                  {
                     ysv_ext0 += 1;
                     dy_ext0 -= 1;
                  }
               }
               else
               {
                  ysv_ext0 = ysv_ext1 = ysv_ext2 = ysb;
                  dy_ext0 = dy_ext1 = dy_ext2 = dy0 - 4 * SQUISH_CONSTANT_4D;
               }

               if ((c & 0x04) != 0)
               {
                  zsv_ext0 = zsv_ext1 = zsv_ext2 = zsb + 1;
                  dz_ext0 = dz_ext1 = dz_ext2 = dz0 - 1 - 4 * SQUISH_CONSTANT_4D;
                  if ((c & 0x03) != 0x03)
                  {
                     if ((c & 0x03) == 0)
                     {
                        zsv_ext0 += 1;
                        dz_ext0 -= 1;
                     }
                     else
                     {
                        zsv_ext1 += 1;
                        dz_ext1 -= 1;
                     }
                  }
                  else
                  {
                     zsv_ext2 += 1;
                     dz_ext2 -= 1;
                  }
               }
               else
               {
                  zsv_ext0 = zsv_ext1 = zsv_ext2 = zsb;
                  dz_ext0 = dz_ext1 = dz_ext2 = dz0 - 4 * SQUISH_CONSTANT_4D;
               }

               if ((c & 0x08) != 0)
               {
                  wsv_ext0 = wsv_ext1 = wsb + 1;
                  wsv_ext2 = wsb + 2;
                  dw_ext0 = dw_ext1 = dw0 - 1 - 4 * SQUISH_CONSTANT_4D;
                  dw_ext2 = dw0 - 2 - 4 * SQUISH_CONSTANT_4D;
               }
               else
               {
                  wsv_ext0 = wsv_ext1 = wsv_ext2 = wsb;
                  dw_ext0 = dw_ext1 = dw_ext2 = dw0 - 4 * SQUISH_CONSTANT_4D;
               }
            }
            else
            { //(1,1,1,1) is not one of the closest two pentachoron vertices.
               int c = (int)(aPoint & bPoint); //Our three extra vertices are determined by the closest two.

               if ((c & 0x01) != 0)
               {
                  xsv_ext0 = xsv_ext2 = xsb + 1;
                  xsv_ext1 = xsb + 2;
                  dx_ext0 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dx_ext1 = dx0 - 2 - 3 * SQUISH_CONSTANT_4D;
                  dx_ext2 = dx0 - 1 - 3 * SQUISH_CONSTANT_4D;
               }
               else
               {
                  xsv_ext0 = xsv_ext1 = xsv_ext2 = xsb;
                  dx_ext0 = dx0 - 2 * SQUISH_CONSTANT_4D;
                  dx_ext1 = dx_ext2 = dx0 - 3 * SQUISH_CONSTANT_4D;
               }

               if ((c & 0x02) != 0)
               {
                  ysv_ext0 = ysv_ext1 = ysv_ext2 = ysb + 1;
                  dy_ext0 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dy_ext1 = dy_ext2 = dy0 - 1 - 3 * SQUISH_CONSTANT_4D;
                  if ((c & 0x01) != 0)
                  {
                     ysv_ext2 += 1;
                     dy_ext2 -= 1;
                  }
                  else
                  {
                     ysv_ext1 += 1;
                     dy_ext1 -= 1;
                  }
               }
               else
               {
                  ysv_ext0 = ysv_ext1 = ysv_ext2 = ysb;
                  dy_ext0 = dy0 - 2 * SQUISH_CONSTANT_4D;
                  dy_ext1 = dy_ext2 = dy0 - 3 * SQUISH_CONSTANT_4D;
               }

               if ((c & 0x04) != 0)
               {
                  zsv_ext0 = zsv_ext1 = zsv_ext2 = zsb + 1;
                  dz_ext0 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dz_ext1 = dz_ext2 = dz0 - 1 - 3 * SQUISH_CONSTANT_4D;
                  if ((c & 0x03) != 0)
                  {
                     zsv_ext2 += 1;
                     dz_ext2 -= 1;
                  }
                  else
                  {
                     zsv_ext1 += 1;
                     dz_ext1 -= 1;
                  }
               }
               else
               {
                  zsv_ext0 = zsv_ext1 = zsv_ext2 = zsb;
                  dz_ext0 = dz0 - 2 * SQUISH_CONSTANT_4D;
                  dz_ext1 = dz_ext2 = dz0 - 3 * SQUISH_CONSTANT_4D;
               }

               if ((c & 0x08) != 0)
               {
                  wsv_ext0 = wsv_ext1 = wsb + 1;
                  wsv_ext2 = wsb + 2;
                  dw_ext0 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dw_ext1 = dw0 - 1 - 3 * SQUISH_CONSTANT_4D;
                  dw_ext2 = dw0 - 2 - 3 * SQUISH_CONSTANT_4D;
               }
               else
               {
                  wsv_ext0 = wsv_ext1 = wsv_ext2 = wsb;
                  dw_ext0 = dw0 - 2 * SQUISH_CONSTANT_4D;
                  dw_ext1 = dw_ext2 = dw0 - 3 * SQUISH_CONSTANT_4D;
               }
            }

            //Contribution (1,1,1,0)
            float dx4 = dx0 - 1 - 3 * SQUISH_CONSTANT_4D;
            float dy4 = dy0 - 1 - 3 * SQUISH_CONSTANT_4D;
            float dz4 = dz0 - 1 - 3 * SQUISH_CONSTANT_4D;
            float dw4 = dw0 - 3 * SQUISH_CONSTANT_4D;
            float attn4 = 2 - dx4 * dx4 - dy4 * dy4 - dz4 * dz4 - dw4 * dw4;
            if (attn4 > 0)
            {
               attn4 *= attn4;
               value += attn4 * attn4 * Extrapolate(xsb + 1, ysb + 1, zsb + 1, wsb + 0, dx4, dy4, dz4, dw4);
            }

            //Contribution (1,1,0,1)
            float dx3 = dx4;
            float dy3 = dy4;
            float dz3 = dz0 - 3 * SQUISH_CONSTANT_4D;
            float dw3 = dw0 - 1 - 3 * SQUISH_CONSTANT_4D;
            float attn3 = 2 - dx3 * dx3 - dy3 * dy3 - dz3 * dz3 - dw3 * dw3;
            if (attn3 > 0)
            {
               attn3 *= attn3;
               value += attn3 * attn3 * Extrapolate(xsb + 1, ysb + 1, zsb + 0, wsb + 1, dx3, dy3, dz3, dw3);
            }

            //Contribution (1,0,1,1)
            float dx2 = dx4;
            float dy2 = dy0 - 3 * SQUISH_CONSTANT_4D;
            float dz2 = dz4;
            float dw2 = dw3;
            float attn2 = 2 - dx2 * dx2 - dy2 * dy2 - dz2 * dz2 - dw2 * dw2;
            if (attn2 > 0)
            {
               attn2 *= attn2;
               value += attn2 * attn2 * Extrapolate(xsb + 1, ysb + 0, zsb + 1, wsb + 1, dx2, dy2, dz2, dw2);
            }

            //Contribution (0,1,1,1)
            float dx1 = dx0 - 3 * SQUISH_CONSTANT_4D;
            float dz1 = dz4;
            float dy1 = dy4;
            float dw1 = dw3;
            float attn1 = 2 - dx1 * dx1 - dy1 * dy1 - dz1 * dz1 - dw1 * dw1;
            if (attn1 > 0)
            {
               attn1 *= attn1;
               value += attn1 * attn1 * Extrapolate(xsb + 0, ysb + 1, zsb + 1, wsb + 1, dx1, dy1, dz1, dw1);
            }

            //Contribution (1,1,1,1)
            dx0 = dx0 - 1 - 4 * SQUISH_CONSTANT_4D;
            dy0 = dy0 - 1 - 4 * SQUISH_CONSTANT_4D;
            dz0 = dz0 - 1 - 4 * SQUISH_CONSTANT_4D;
            dw0 = dw0 - 1 - 4 * SQUISH_CONSTANT_4D;
            float attn0 = 2 - dx0 * dx0 - dy0 * dy0 - dz0 * dz0 - dw0 * dw0;
            if (attn0 > 0)
            {
               attn0 *= attn0;
               value += attn0 * attn0 * Extrapolate(xsb + 1, ysb + 1, zsb + 1, wsb + 1, dx0, dy0, dz0, dw0);
            }
         }
         else if (inSum <= 2)
         { //We're inside the first dispentachoron (Rectified 4-Simplex)
            float aScore;
            int aPoint;
            bool aIsBiggerSide = true;
            float bScore;
            int bPoint;
            bool bIsBiggerSide = true;

            //Decide between (1,1,0,0) and (0,0,1,1)
            if (xins + yins > zins + wins)
            {
               aScore = xins + yins;
               aPoint = 0x03;
            }
            else
            {
               aScore = zins + wins;
               aPoint = 0x0C;
            }

            //Decide between (1,0,1,0) and (0,1,0,1)
            if (xins + zins > yins + wins)
            {
               bScore = xins + zins;
               bPoint = 0x05;
            }
            else
            {
               bScore = yins + wins;
               bPoint = 0x0A;
            }

            //Closer between (1,0,0,1) and (0,1,1,0) will replace the further of a and b, if closer.
            if (xins + wins > yins + zins)
            {
               float score = xins + wins;
               if (aScore >= bScore && score > bScore)
               {
                  bScore = score;
                  bPoint = 0x09;
               }
               else if (aScore < bScore && score > aScore)
               {
                  aScore = score;
                  aPoint = 0x09;
               }
            }
            else
            {
               float score = yins + zins;
               if (aScore >= bScore && score > bScore)
               {
                  bScore = score;
                  bPoint = 0x06;
               }
               else if (aScore < bScore && score > aScore)
               {
                  aScore = score;
                  aPoint = 0x06;
               }
            }

            //Decide if (1,0,0,0) is closer.
            float p1 = 2 - inSum + xins;
            if (aScore >= bScore && p1 > bScore)
            {
               bScore = p1;
               bPoint = 0x01;
               bIsBiggerSide = false;
            }
            else if (aScore < bScore && p1 > aScore)
            {
               aScore = p1;
               aPoint = 0x01;
               aIsBiggerSide = false;
            }

            //Decide if (0,1,0,0) is closer.
            float p2 = 2 - inSum + yins;
            if (aScore >= bScore && p2 > bScore)
            {
               bScore = p2;
               bPoint = 0x02;
               bIsBiggerSide = false;
            }
            else if (aScore < bScore && p2 > aScore)
            {
               aScore = p2;
               aPoint = 0x02;
               aIsBiggerSide = false;
            }

            //Decide if (0,0,1,0) is closer.
            float p3 = 2 - inSum + zins;
            if (aScore >= bScore && p3 > bScore)
            {
               bScore = p3;
               bPoint = 0x04;
               bIsBiggerSide = false;
            }
            else if (aScore < bScore && p3 > aScore)
            {
               aScore = p3;
               aPoint = 0x04;
               aIsBiggerSide = false;
            }

            //Decide if (0,0,0,1) is closer.
            float p4 = 2 - inSum + wins;
            if (aScore >= bScore && p4 > bScore)
            {
               bScore = p4;
               bPoint = 0x08;
               bIsBiggerSide = false;
            }
            else if (aScore < bScore && p4 > aScore)
            {
               aScore = p4;
               aPoint = 0x08;
               aIsBiggerSide = false;
            }

            //Where each of the two closest points are determines how the extra three vertices are calculated.
            if (aIsBiggerSide == bIsBiggerSide)
            {
               if (aIsBiggerSide)
               { //Both closest points on the bigger side
                  int c1 = (int)(aPoint | bPoint);
                  int c2 = (int)(aPoint & bPoint);
                  if ((c1 & 0x01) == 0)
                  {
                     xsv_ext0 = xsb;
                     xsv_ext1 = xsb - 1;
                     dx_ext0 = dx0 - 3 * SQUISH_CONSTANT_4D;
                     dx_ext1 = dx0 + 1 - 2 * SQUISH_CONSTANT_4D;
                  }
                  else
                  {
                     xsv_ext0 = xsv_ext1 = xsb + 1;
                     dx_ext0 = dx0 - 1 - 3 * SQUISH_CONSTANT_4D;
                     dx_ext1 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  }

                  if ((c1 & 0x02) == 0)
                  {
                     ysv_ext0 = ysb;
                     ysv_ext1 = ysb - 1;
                     dy_ext0 = dy0 - 3 * SQUISH_CONSTANT_4D;
                     dy_ext1 = dy0 + 1 - 2 * SQUISH_CONSTANT_4D;
                  }
                  else
                  {
                     ysv_ext0 = ysv_ext1 = ysb + 1;
                     dy_ext0 = dy0 - 1 - 3 * SQUISH_CONSTANT_4D;
                     dy_ext1 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  }

                  if ((c1 & 0x04) == 0)
                  {
                     zsv_ext0 = zsb;
                     zsv_ext1 = zsb - 1;
                     dz_ext0 = dz0 - 3 * SQUISH_CONSTANT_4D;
                     dz_ext1 = dz0 + 1 - 2 * SQUISH_CONSTANT_4D;
                  }
                  else
                  {
                     zsv_ext0 = zsv_ext1 = zsb + 1;
                     dz_ext0 = dz0 - 1 - 3 * SQUISH_CONSTANT_4D;
                     dz_ext1 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  }

                  if ((c1 & 0x08) == 0)
                  {
                     wsv_ext0 = wsb;
                     wsv_ext1 = wsb - 1;
                     dw_ext0 = dw0 - 3 * SQUISH_CONSTANT_4D;
                     dw_ext1 = dw0 + 1 - 2 * SQUISH_CONSTANT_4D;
                  }
                  else
                  {
                     wsv_ext0 = wsv_ext1 = wsb + 1;
                     dw_ext0 = dw0 - 1 - 3 * SQUISH_CONSTANT_4D;
                     dw_ext1 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  }

                  //One combination is a permutation of (0,0,0,2) based on c2
                  xsv_ext2 = xsb;
                  ysv_ext2 = ysb;
                  zsv_ext2 = zsb;
                  wsv_ext2 = wsb;
                  dx_ext2 = dx0 - 2 * SQUISH_CONSTANT_4D;
                  dy_ext2 = dy0 - 2 * SQUISH_CONSTANT_4D;
                  dz_ext2 = dz0 - 2 * SQUISH_CONSTANT_4D;
                  dw_ext2 = dw0 - 2 * SQUISH_CONSTANT_4D;
                  if ((c2 & 0x01) != 0)
                  {
                     xsv_ext2 += 2;
                     dx_ext2 -= 2;
                  }
                  else if ((c2 & 0x02) != 0)
                  {
                     ysv_ext2 += 2;
                     dy_ext2 -= 2;
                  }
                  else if ((c2 & 0x04) != 0)
                  {
                     zsv_ext2 += 2;
                     dz_ext2 -= 2;
                  }
                  else
                  {
                     wsv_ext2 += 2;
                     dw_ext2 -= 2;
                  }

               }
               else
               { //Both closest points on the smaller side
                 //One of the two extra points is (0,0,0,0)
                  xsv_ext2 = xsb;
                  ysv_ext2 = ysb;
                  zsv_ext2 = zsb;
                  wsv_ext2 = wsb;
                  dx_ext2 = dx0;
                  dy_ext2 = dy0;
                  dz_ext2 = dz0;
                  dw_ext2 = dw0;

                  //Other two points are based on the omitted axes.
                  int c = (int)(aPoint | bPoint);

                  if ((c & 0x01) == 0)
                  {
                     xsv_ext0 = xsb - 1;
                     xsv_ext1 = xsb;
                     dx_ext0 = dx0 + 1 - SQUISH_CONSTANT_4D;
                     dx_ext1 = dx0 - SQUISH_CONSTANT_4D;
                  }
                  else
                  {
                     xsv_ext0 = xsv_ext1 = xsb + 1;
                     dx_ext0 = dx_ext1 = dx0 - 1 - SQUISH_CONSTANT_4D;
                  }

                  if ((c & 0x02) == 0)
                  {
                     ysv_ext0 = ysv_ext1 = ysb;
                     dy_ext0 = dy_ext1 = dy0 - SQUISH_CONSTANT_4D;
                     if ((c & 0x01) == 0x01)
                     {
                        ysv_ext0 -= 1;
                        dy_ext0 += 1;
                     }
                     else
                     {
                        ysv_ext1 -= 1;
                        dy_ext1 += 1;
                     }
                  }
                  else
                  {
                     ysv_ext0 = ysv_ext1 = ysb + 1;
                     dy_ext0 = dy_ext1 = dy0 - 1 - SQUISH_CONSTANT_4D;
                  }

                  if ((c & 0x04) == 0)
                  {
                     zsv_ext0 = zsv_ext1 = zsb;
                     dz_ext0 = dz_ext1 = dz0 - SQUISH_CONSTANT_4D;
                     if ((c & 0x03) == 0x03)
                     {
                        zsv_ext0 -= 1;
                        dz_ext0 += 1;
                     }
                     else
                     {
                        zsv_ext1 -= 1;
                        dz_ext1 += 1;
                     }
                  }
                  else
                  {
                     zsv_ext0 = zsv_ext1 = zsb + 1;
                     dz_ext0 = dz_ext1 = dz0 - 1 - SQUISH_CONSTANT_4D;
                  }

                  if ((c & 0x08) == 0)
                  {
                     wsv_ext0 = wsb;
                     wsv_ext1 = wsb - 1;
                     dw_ext0 = dw0 - SQUISH_CONSTANT_4D;
                     dw_ext1 = dw0 + 1 - SQUISH_CONSTANT_4D;
                  }
                  else
                  {
                     wsv_ext0 = wsv_ext1 = wsb + 1;
                     dw_ext0 = dw_ext1 = dw0 - 1 - SQUISH_CONSTANT_4D;
                  }

               }
            }
            else
            { //One point on each "side"
               int c1, c2;
               if (aIsBiggerSide)
               {
                  c1 = aPoint;
                  c2 = bPoint;
               }
               else
               {
                  c1 = bPoint;
                  c2 = aPoint;
               }

               //Two contributions are the bigger-sided point with each 0 replaced with -1.
               if ((c1 & 0x01) == 0)
               {
                  xsv_ext0 = xsb - 1;
                  xsv_ext1 = xsb;
                  dx_ext0 = dx0 + 1 - SQUISH_CONSTANT_4D;
                  dx_ext1 = dx0 - SQUISH_CONSTANT_4D;
               }
               else
               {
                  xsv_ext0 = xsv_ext1 = xsb + 1;
                  dx_ext0 = dx_ext1 = dx0 - 1 - SQUISH_CONSTANT_4D;
               }

               if ((c1 & 0x02) == 0)
               {
                  ysv_ext0 = ysv_ext1 = ysb;
                  dy_ext0 = dy_ext1 = dy0 - SQUISH_CONSTANT_4D;
                  if ((c1 & 0x01) == 0x01)
                  {
                     ysv_ext0 -= 1;
                     dy_ext0 += 1;
                  }
                  else
                  {
                     ysv_ext1 -= 1;
                     dy_ext1 += 1;
                  }
               }
               else
               {
                  ysv_ext0 = ysv_ext1 = ysb + 1;
                  dy_ext0 = dy_ext1 = dy0 - 1 - SQUISH_CONSTANT_4D;
               }

               if ((c1 & 0x04) == 0)
               {
                  zsv_ext0 = zsv_ext1 = zsb;
                  dz_ext0 = dz_ext1 = dz0 - SQUISH_CONSTANT_4D;
                  if ((c1 & 0x03) == 0x03)
                  {
                     zsv_ext0 -= 1;
                     dz_ext0 += 1;
                  }
                  else
                  {
                     zsv_ext1 -= 1;
                     dz_ext1 += 1;
                  }
               }
               else
               {
                  zsv_ext0 = zsv_ext1 = zsb + 1;
                  dz_ext0 = dz_ext1 = dz0 - 1 - SQUISH_CONSTANT_4D;
               }

               if ((c1 & 0x08) == 0)
               {
                  wsv_ext0 = wsb;
                  wsv_ext1 = wsb - 1;
                  dw_ext0 = dw0 - SQUISH_CONSTANT_4D;
                  dw_ext1 = dw0 + 1 - SQUISH_CONSTANT_4D;
               }
               else
               {
                  wsv_ext0 = wsv_ext1 = wsb + 1;
                  dw_ext0 = dw_ext1 = dw0 - 1 - SQUISH_CONSTANT_4D;
               }

               //One contribution is a permutation of (0,0,0,2) based on the smaller-sided point
               xsv_ext2 = xsb;
               ysv_ext2 = ysb;
               zsv_ext2 = zsb;
               wsv_ext2 = wsb;
               dx_ext2 = dx0 - 2 * SQUISH_CONSTANT_4D;
               dy_ext2 = dy0 - 2 * SQUISH_CONSTANT_4D;
               dz_ext2 = dz0 - 2 * SQUISH_CONSTANT_4D;
               dw_ext2 = dw0 - 2 * SQUISH_CONSTANT_4D;
               if ((c2 & 0x01) != 0)
               {
                  xsv_ext2 += 2;
                  dx_ext2 -= 2;
               }
               else if ((c2 & 0x02) != 0)
               {
                  ysv_ext2 += 2;
                  dy_ext2 -= 2;
               }
               else if ((c2 & 0x04) != 0)
               {
                  zsv_ext2 += 2;
                  dz_ext2 -= 2;
               }
               else
               {
                  wsv_ext2 += 2;
                  dw_ext2 -= 2;
               }
            }

            //Contribution (1,0,0,0)
            float dx1 = dx0 - 1 - SQUISH_CONSTANT_4D;
            float dy1 = dy0 - 0 - SQUISH_CONSTANT_4D;
            float dz1 = dz0 - 0 - SQUISH_CONSTANT_4D;
            float dw1 = dw0 - 0 - SQUISH_CONSTANT_4D;
            float attn1 = 2 - dx1 * dx1 - dy1 * dy1 - dz1 * dz1 - dw1 * dw1;
            if (attn1 > 0)
            {
               attn1 *= attn1;
               value += attn1 * attn1 * Extrapolate(xsb + 1, ysb + 0, zsb + 0, wsb + 0, dx1, dy1, dz1, dw1);
            }

            //Contribution (0,1,0,0)
            float dx2 = dx0 - 0 - SQUISH_CONSTANT_4D;
            float dy2 = dy0 - 1 - SQUISH_CONSTANT_4D;
            float dz2 = dz1;
            float dw2 = dw1;
            float attn2 = 2 - dx2 * dx2 - dy2 * dy2 - dz2 * dz2 - dw2 * dw2;
            if (attn2 > 0)
            {
               attn2 *= attn2;
               value += attn2 * attn2 * Extrapolate(xsb + 0, ysb + 1, zsb + 0, wsb + 0, dx2, dy2, dz2, dw2);
            }

            //Contribution (0,0,1,0)
            float dx3 = dx2;
            float dy3 = dy1;
            float dz3 = dz0 - 1 - SQUISH_CONSTANT_4D;
            float dw3 = dw1;
            float attn3 = 2 - dx3 * dx3 - dy3 * dy3 - dz3 * dz3 - dw3 * dw3;
            if (attn3 > 0)
            {
               attn3 *= attn3;
               value += attn3 * attn3 * Extrapolate(xsb + 0, ysb + 0, zsb + 1, wsb + 0, dx3, dy3, dz3, dw3);
            }

            //Contribution (0,0,0,1)
            float dx4 = dx2;
            float dy4 = dy1;
            float dz4 = dz1;
            float dw4 = dw0 - 1 - SQUISH_CONSTANT_4D;
            float attn4 = 2 - dx4 * dx4 - dy4 * dy4 - dz4 * dz4 - dw4 * dw4;
            if (attn4 > 0)
            {
               attn4 *= attn4;
               value += attn4 * attn4 * Extrapolate(xsb + 0, ysb + 0, zsb + 0, wsb + 1, dx4, dy4, dz4, dw4);
            }

            //Contribution (1,1,0,0)
            float dx5 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dy5 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dz5 = dz0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dw5 = dw0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float attn5 = 2 - dx5 * dx5 - dy5 * dy5 - dz5 * dz5 - dw5 * dw5;
            if (attn5 > 0)
            {
               attn5 *= attn5;
               value += attn5 * attn5 * Extrapolate(xsb + 1, ysb + 1, zsb + 0, wsb + 0, dx5, dy5, dz5, dw5);
            }

            //Contribution (1,0,1,0)
            float dx6 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dy6 = dy0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dz6 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dw6 = dw0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float attn6 = 2 - dx6 * dx6 - dy6 * dy6 - dz6 * dz6 - dw6 * dw6;
            if (attn6 > 0)
            {
               attn6 *= attn6;
               value += attn6 * attn6 * Extrapolate(xsb + 1, ysb + 0, zsb + 1, wsb + 0, dx6, dy6, dz6, dw6);
            }

            //Contribution (1,0,0,1)
            float dx7 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dy7 = dy0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dz7 = dz0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dw7 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float attn7 = 2 - dx7 * dx7 - dy7 * dy7 - dz7 * dz7 - dw7 * dw7;
            if (attn7 > 0)
            {
               attn7 *= attn7;
               value += attn7 * attn7 * Extrapolate(xsb + 1, ysb + 0, zsb + 0, wsb + 1, dx7, dy7, dz7, dw7);
            }

            //Contribution (0,1,1,0)
            float dx8 = dx0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dy8 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dz8 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dw8 = dw0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float attn8 = 2 - dx8 * dx8 - dy8 * dy8 - dz8 * dz8 - dw8 * dw8;
            if (attn8 > 0)
            {
               attn8 *= attn8;
               value += attn8 * attn8 * Extrapolate(xsb + 0, ysb + 1, zsb + 1, wsb + 0, dx8, dy8, dz8, dw8);
            }

            //Contribution (0,1,0,1)
            float dx9 = dx0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dy9 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dz9 = dz0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dw9 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float attn9 = 2 - dx9 * dx9 - dy9 * dy9 - dz9 * dz9 - dw9 * dw9;
            if (attn9 > 0)
            {
               attn9 *= attn9;
               value += attn9 * attn9 * Extrapolate(xsb + 0, ysb + 1, zsb + 0, wsb + 1, dx9, dy9, dz9, dw9);
            }

            //Contribution (0,0,1,1)
            float dx10 = dx0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dy10 = dy0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dz10 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dw10 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float attn10 = 2 - dx10 * dx10 - dy10 * dy10 - dz10 * dz10 - dw10 * dw10;
            if (attn10 > 0)
            {
               attn10 *= attn10;
               value += attn10 * attn10 * Extrapolate(xsb + 0, ysb + 0, zsb + 1, wsb + 1, dx10, dy10, dz10, dw10);
            }
         }
         else
         { //We're inside the second dispentachoron (Rectified 4-Simplex)
            float aScore;
            int aPoint;
            bool aIsBiggerSide = true;
            float bScore;
            int bPoint;
            bool bIsBiggerSide = true;

            //Decide between (0,0,1,1) and (1,1,0,0)
            if (xins + yins < zins + wins)
            {
               aScore = xins + yins;
               aPoint = 0x0C;
            }
            else
            {
               aScore = zins + wins;
               aPoint = 0x03;
            }

            //Decide between (0,1,0,1) and (1,0,1,0)
            if (xins + zins < yins + wins)
            {
               bScore = xins + zins;
               bPoint = 0x0A;
            }
            else
            {
               bScore = yins + wins;
               bPoint = 0x05;
            }

            //Closer between (0,1,1,0) and (1,0,0,1) will replace the further of a and b, if closer.
            if (xins + wins < yins + zins)
            {
               float score = xins + wins;
               if (aScore <= bScore && score < bScore)
               {
                  bScore = score;
                  bPoint = 0x06;
               }
               else if (aScore > bScore && score < aScore)
               {
                  aScore = score;
                  aPoint = 0x06;
               }
            }
            else
            {
               float score = yins + zins;
               if (aScore <= bScore && score < bScore)
               {
                  bScore = score;
                  bPoint = 0x09;
               }
               else if (aScore > bScore && score < aScore)
               {
                  aScore = score;
                  aPoint = 0x09;
               }
            }

            //Decide if (0,1,1,1) is closer.
            float p1 = 3 - inSum + xins;
            if (aScore <= bScore && p1 < bScore)
            {
               bScore = p1;
               bPoint = 0x0E;
               bIsBiggerSide = false;
            }
            else if (aScore > bScore && p1 < aScore)
            {
               aScore = p1;
               aPoint = 0x0E;
               aIsBiggerSide = false;
            }

            //Decide if (1,0,1,1) is closer.
            float p2 = 3 - inSum + yins;
            if (aScore <= bScore && p2 < bScore)
            {
               bScore = p2;
               bPoint = 0x0D;
               bIsBiggerSide = false;
            }
            else if (aScore > bScore && p2 < aScore)
            {
               aScore = p2;
               aPoint = 0x0D;
               aIsBiggerSide = false;
            }

            //Decide if (1,1,0,1) is closer.
            float p3 = 3 - inSum + zins;
            if (aScore <= bScore && p3 < bScore)
            {
               bScore = p3;
               bPoint = 0x0B;
               bIsBiggerSide = false;
            }
            else if (aScore > bScore && p3 < aScore)
            {
               aScore = p3;
               aPoint = 0x0B;
               aIsBiggerSide = false;
            }

            //Decide if (1,1,1,0) is closer.
            float p4 = 3 - inSum + wins;
            if (aScore <= bScore && p4 < bScore)
            {
               bScore = p4;
               bPoint = 0x07;
               bIsBiggerSide = false;
            }
            else if (aScore > bScore && p4 < aScore)
            {
               aScore = p4;
               aPoint = 0x07;
               aIsBiggerSide = false;
            }

            //Where each of the two closest points are determines how the extra three vertices are calculated.
            if (aIsBiggerSide == bIsBiggerSide)
            {
               if (aIsBiggerSide)
               { //Both closest points on the bigger side
                  int c1 = (int)(aPoint & bPoint);
                  int c2 = (int)(aPoint | bPoint);

                  //Two contributions are permutations of (0,0,0,1) and (0,0,0,2) based on c1
                  xsv_ext0 = xsv_ext1 = xsb;
                  ysv_ext0 = ysv_ext1 = ysb;
                  zsv_ext0 = zsv_ext1 = zsb;
                  wsv_ext0 = wsv_ext1 = wsb;
                  dx_ext0 = dx0 - SQUISH_CONSTANT_4D;
                  dy_ext0 = dy0 - SQUISH_CONSTANT_4D;
                  dz_ext0 = dz0 - SQUISH_CONSTANT_4D;
                  dw_ext0 = dw0 - SQUISH_CONSTANT_4D;
                  dx_ext1 = dx0 - 2 * SQUISH_CONSTANT_4D;
                  dy_ext1 = dy0 - 2 * SQUISH_CONSTANT_4D;
                  dz_ext1 = dz0 - 2 * SQUISH_CONSTANT_4D;
                  dw_ext1 = dw0 - 2 * SQUISH_CONSTANT_4D;
                  if ((c1 & 0x01) != 0)
                  {
                     xsv_ext0 += 1;
                     dx_ext0 -= 1;
                     xsv_ext1 += 2;
                     dx_ext1 -= 2;
                  }
                  else if ((c1 & 0x02) != 0)
                  {
                     ysv_ext0 += 1;
                     dy_ext0 -= 1;
                     ysv_ext1 += 2;
                     dy_ext1 -= 2;
                  }
                  else if ((c1 & 0x04) != 0)
                  {
                     zsv_ext0 += 1;
                     dz_ext0 -= 1;
                     zsv_ext1 += 2;
                     dz_ext1 -= 2;
                  }
                  else
                  {
                     wsv_ext0 += 1;
                     dw_ext0 -= 1;
                     wsv_ext1 += 2;
                     dw_ext1 -= 2;
                  }

                  //One contribution is a permutation of (1,1,1,-1) based on c2
                  xsv_ext2 = xsb + 1;
                  ysv_ext2 = ysb + 1;
                  zsv_ext2 = zsb + 1;
                  wsv_ext2 = wsb + 1;
                  dx_ext2 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dy_ext2 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dz_ext2 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  dw_ext2 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
                  if ((c2 & 0x01) == 0)
                  {
                     xsv_ext2 -= 2;
                     dx_ext2 += 2;
                  }
                  else if ((c2 & 0x02) == 0)
                  {
                     ysv_ext2 -= 2;
                     dy_ext2 += 2;
                  }
                  else if ((c2 & 0x04) == 0)
                  {
                     zsv_ext2 -= 2;
                     dz_ext2 += 2;
                  }
                  else
                  {
                     wsv_ext2 -= 2;
                     dw_ext2 += 2;
                  }
               }
               else
               { //Both closest points on the smaller side
                 //One of the two extra points is (1,1,1,1)
                  xsv_ext2 = xsb + 1;
                  ysv_ext2 = ysb + 1;
                  zsv_ext2 = zsb + 1;
                  wsv_ext2 = wsb + 1;
                  dx_ext2 = dx0 - 1 - 4 * SQUISH_CONSTANT_4D;
                  dy_ext2 = dy0 - 1 - 4 * SQUISH_CONSTANT_4D;
                  dz_ext2 = dz0 - 1 - 4 * SQUISH_CONSTANT_4D;
                  dw_ext2 = dw0 - 1 - 4 * SQUISH_CONSTANT_4D;

                  //Other two points are based on the shared axes.
                  int c = (int)(aPoint & bPoint);

                  if ((c & 0x01) != 0)
                  {
                     xsv_ext0 = xsb + 2;
                     xsv_ext1 = xsb + 1;
                     dx_ext0 = dx0 - 2 - 3 * SQUISH_CONSTANT_4D;
                     dx_ext1 = dx0 - 1 - 3 * SQUISH_CONSTANT_4D;
                  }
                  else
                  {
                     xsv_ext0 = xsv_ext1 = xsb;
                     dx_ext0 = dx_ext1 = dx0 - 3 * SQUISH_CONSTANT_4D;
                  }

                  if ((c & 0x02) != 0)
                  {
                     ysv_ext0 = ysv_ext1 = ysb + 1;
                     dy_ext0 = dy_ext1 = dy0 - 1 - 3 * SQUISH_CONSTANT_4D;
                     if ((c & 0x01) == 0)
                     {
                        ysv_ext0 += 1;
                        dy_ext0 -= 1;
                     }
                     else
                     {
                        ysv_ext1 += 1;
                        dy_ext1 -= 1;
                     }
                  }
                  else
                  {
                     ysv_ext0 = ysv_ext1 = ysb;
                     dy_ext0 = dy_ext1 = dy0 - 3 * SQUISH_CONSTANT_4D;
                  }

                  if ((c & 0x04) != 0)
                  {
                     zsv_ext0 = zsv_ext1 = zsb + 1;
                     dz_ext0 = dz_ext1 = dz0 - 1 - 3 * SQUISH_CONSTANT_4D;
                     if ((c & 0x03) == 0)
                     {
                        zsv_ext0 += 1;
                        dz_ext0 -= 1;
                     }
                     else
                     {
                        zsv_ext1 += 1;
                        dz_ext1 -= 1;
                     }
                  }
                  else
                  {
                     zsv_ext0 = zsv_ext1 = zsb;
                     dz_ext0 = dz_ext1 = dz0 - 3 * SQUISH_CONSTANT_4D;
                  }

                  if ((c & 0x08) != 0)
                  {
                     wsv_ext0 = wsb + 1;
                     wsv_ext1 = wsb + 2;
                     dw_ext0 = dw0 - 1 - 3 * SQUISH_CONSTANT_4D;
                     dw_ext1 = dw0 - 2 - 3 * SQUISH_CONSTANT_4D;
                  }
                  else
                  {
                     wsv_ext0 = wsv_ext1 = wsb;
                     dw_ext0 = dw_ext1 = dw0 - 3 * SQUISH_CONSTANT_4D;
                  }
               }
            }
            else
            { //One point on each "side"
               int c1, c2;
               if (aIsBiggerSide)
               {
                  c1 = aPoint;
                  c2 = bPoint;
               }
               else
               {
                  c1 = bPoint;
                  c2 = aPoint;
               }

               //Two contributions are the bigger-sided point with each 1 replaced with 2.
               if ((c1 & 0x01) != 0)
               {
                  xsv_ext0 = xsb + 2;
                  xsv_ext1 = xsb + 1;
                  dx_ext0 = dx0 - 2 - 3 * SQUISH_CONSTANT_4D;
                  dx_ext1 = dx0 - 1 - 3 * SQUISH_CONSTANT_4D;
               }
               else
               {
                  xsv_ext0 = xsv_ext1 = xsb;
                  dx_ext0 = dx_ext1 = dx0 - 3 * SQUISH_CONSTANT_4D;
               }

               if ((c1 & 0x02) != 0)
               {
                  ysv_ext0 = ysv_ext1 = ysb + 1;
                  dy_ext0 = dy_ext1 = dy0 - 1 - 3 * SQUISH_CONSTANT_4D;
                  if ((c1 & 0x01) == 0)
                  {
                     ysv_ext0 += 1;
                     dy_ext0 -= 1;
                  }
                  else
                  {
                     ysv_ext1 += 1;
                     dy_ext1 -= 1;
                  }
               }
               else
               {
                  ysv_ext0 = ysv_ext1 = ysb;
                  dy_ext0 = dy_ext1 = dy0 - 3 * SQUISH_CONSTANT_4D;
               }

               if ((c1 & 0x04) != 0)
               {
                  zsv_ext0 = zsv_ext1 = zsb + 1;
                  dz_ext0 = dz_ext1 = dz0 - 1 - 3 * SQUISH_CONSTANT_4D;
                  if ((c1 & 0x03) == 0)
                  {
                     zsv_ext0 += 1;
                     dz_ext0 -= 1;
                  }
                  else
                  {
                     zsv_ext1 += 1;
                     dz_ext1 -= 1;
                  }
               }
               else
               {
                  zsv_ext0 = zsv_ext1 = zsb;
                  dz_ext0 = dz_ext1 = dz0 - 3 * SQUISH_CONSTANT_4D;
               }

               if ((c1 & 0x08) != 0)
               {
                  wsv_ext0 = wsb + 1;
                  wsv_ext1 = wsb + 2;
                  dw_ext0 = dw0 - 1 - 3 * SQUISH_CONSTANT_4D;
                  dw_ext1 = dw0 - 2 - 3 * SQUISH_CONSTANT_4D;
               }
               else
               {
                  wsv_ext0 = wsv_ext1 = wsb;
                  dw_ext0 = dw_ext1 = dw0 - 3 * SQUISH_CONSTANT_4D;
               }

               //One contribution is a permutation of (1,1,1,-1) based on the smaller-sided point
               xsv_ext2 = xsb + 1;
               ysv_ext2 = ysb + 1;
               zsv_ext2 = zsb + 1;
               wsv_ext2 = wsb + 1;
               dx_ext2 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
               dy_ext2 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
               dz_ext2 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
               dw_ext2 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
               if ((c2 & 0x01) == 0)
               {
                  xsv_ext2 -= 2;
                  dx_ext2 += 2;
               }
               else if ((c2 & 0x02) == 0)
               {
                  ysv_ext2 -= 2;
                  dy_ext2 += 2;
               }
               else if ((c2 & 0x04) == 0)
               {
                  zsv_ext2 -= 2;
                  dz_ext2 += 2;
               }
               else
               {
                  wsv_ext2 -= 2;
                  dw_ext2 += 2;
               }
            }

            //Contribution (1,1,1,0)
            float dx4 = dx0 - 1 - 3 * SQUISH_CONSTANT_4D;
            float dy4 = dy0 - 1 - 3 * SQUISH_CONSTANT_4D;
            float dz4 = dz0 - 1 - 3 * SQUISH_CONSTANT_4D;
            float dw4 = dw0 - 3 * SQUISH_CONSTANT_4D;
            float attn4 = 2 - dx4 * dx4 - dy4 * dy4 - dz4 * dz4 - dw4 * dw4;
            if (attn4 > 0)
            {
               attn4 *= attn4;
               value += attn4 * attn4 * Extrapolate(xsb + 1, ysb + 1, zsb + 1, wsb + 0, dx4, dy4, dz4, dw4);
            }

            //Contribution (1,1,0,1)
            float dx3 = dx4;
            float dy3 = dy4;
            float dz3 = dz0 - 3 * SQUISH_CONSTANT_4D;
            float dw3 = dw0 - 1 - 3 * SQUISH_CONSTANT_4D;
            float attn3 = 2 - dx3 * dx3 - dy3 * dy3 - dz3 * dz3 - dw3 * dw3;
            if (attn3 > 0)
            {
               attn3 *= attn3;
               value += attn3 * attn3 * Extrapolate(xsb + 1, ysb + 1, zsb + 0, wsb + 1, dx3, dy3, dz3, dw3);
            }

            //Contribution (1,0,1,1)
            float dx2 = dx4;
            float dy2 = dy0 - 3 * SQUISH_CONSTANT_4D;
            float dz2 = dz4;
            float dw2 = dw3;
            float attn2 = 2 - dx2 * dx2 - dy2 * dy2 - dz2 * dz2 - dw2 * dw2;
            if (attn2 > 0)
            {
               attn2 *= attn2;
               value += attn2 * attn2 * Extrapolate(xsb + 1, ysb + 0, zsb + 1, wsb + 1, dx2, dy2, dz2, dw2);
            }

            //Contribution (0,1,1,1)
            float dx1 = dx0 - 3 * SQUISH_CONSTANT_4D;
            float dz1 = dz4;
            float dy1 = dy4;
            float dw1 = dw3;
            float attn1 = 2 - dx1 * dx1 - dy1 * dy1 - dz1 * dz1 - dw1 * dw1;
            if (attn1 > 0)
            {
               attn1 *= attn1;
               value += attn1 * attn1 * Extrapolate(xsb + 0, ysb + 1, zsb + 1, wsb + 1, dx1, dy1, dz1, dw1);
            }

            //Contribution (1,1,0,0)
            float dx5 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dy5 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dz5 = dz0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dw5 = dw0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float attn5 = 2 - dx5 * dx5 - dy5 * dy5 - dz5 * dz5 - dw5 * dw5;
            if (attn5 > 0)
            {
               attn5 *= attn5;
               value += attn5 * attn5 * Extrapolate(xsb + 1, ysb + 1, zsb + 0, wsb + 0, dx5, dy5, dz5, dw5);
            }

            //Contribution (1,0,1,0)
            float dx6 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dy6 = dy0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dz6 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dw6 = dw0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float attn6 = 2 - dx6 * dx6 - dy6 * dy6 - dz6 * dz6 - dw6 * dw6;
            if (attn6 > 0)
            {
               attn6 *= attn6;
               value += attn6 * attn6 * Extrapolate(xsb + 1, ysb + 0, zsb + 1, wsb + 0, dx6, dy6, dz6, dw6);
            }

            //Contribution (1,0,0,1)
            float dx7 = dx0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dy7 = dy0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dz7 = dz0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dw7 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float attn7 = 2 - dx7 * dx7 - dy7 * dy7 - dz7 * dz7 - dw7 * dw7;
            if (attn7 > 0)
            {
               attn7 *= attn7;
               value += attn7 * attn7 * Extrapolate(xsb + 1, ysb + 0, zsb + 0, wsb + 1, dx7, dy7, dz7, dw7);
            }

            //Contribution (0,1,1,0)
            float dx8 = dx0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dy8 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dz8 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dw8 = dw0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float attn8 = 2 - dx8 * dx8 - dy8 * dy8 - dz8 * dz8 - dw8 * dw8;
            if (attn8 > 0)
            {
               attn8 *= attn8;
               value += attn8 * attn8 * Extrapolate(xsb + 0, ysb + 1, zsb + 1, wsb + 0, dx8, dy8, dz8, dw8);
            }

            //Contribution (0,1,0,1)
            float dx9 = dx0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dy9 = dy0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dz9 = dz0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dw9 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float attn9 = 2 - dx9 * dx9 - dy9 * dy9 - dz9 * dz9 - dw9 * dw9;
            if (attn9 > 0)
            {
               attn9 *= attn9;
               value += attn9 * attn9 * Extrapolate(xsb + 0, ysb + 1, zsb + 0, wsb + 1, dx9, dy9, dz9, dw9);
            }

            //Contribution (0,0,1,1)
            float dx10 = dx0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dy10 = dy0 - 0 - 2 * SQUISH_CONSTANT_4D;
            float dz10 = dz0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float dw10 = dw0 - 1 - 2 * SQUISH_CONSTANT_4D;
            float attn10 = 2 - dx10 * dx10 - dy10 * dy10 - dz10 * dz10 - dw10 * dw10;
            if (attn10 > 0)
            {
               attn10 *= attn10;
               value += attn10 * attn10 * Extrapolate(xsb + 0, ysb + 0, zsb + 1, wsb + 1, dx10, dy10, dz10, dw10);
            }
         }

         //First extra vertex
         float attnExt0 = 2 - dx_ext0 * dx_ext0 - dy_ext0 * dy_ext0 - dz_ext0 * dz_ext0 - dw_ext0 * dw_ext0;
         if (attnExt0 > 0)
         {
            attnExt0 *= attnExt0;
            value += attnExt0 * attnExt0 * Extrapolate(xsv_ext0, ysv_ext0, zsv_ext0, wsv_ext0, dx_ext0, dy_ext0, dz_ext0, dw_ext0);
         }

         //Second extra vertex
         float attnExt1 = 2 - dx_ext1 * dx_ext1 - dy_ext1 * dy_ext1 - dz_ext1 * dz_ext1 - dw_ext1 * dw_ext1;
         if (attnExt1 > 0)
         {
            attnExt1 *= attnExt1;
            value += attnExt1 * attnExt1 * Extrapolate(xsv_ext1, ysv_ext1, zsv_ext1, wsv_ext1, dx_ext1, dy_ext1, dz_ext1, dw_ext1);
         }

         //Third extra vertex
         float attnExt2 = 2 - dx_ext2 * dx_ext2 - dy_ext2 * dy_ext2 - dz_ext2 * dz_ext2 - dw_ext2 * dw_ext2;
         if (attnExt2 > 0)
         {
            attnExt2 *= attnExt2;
            value += attnExt2 * attnExt2 * Extrapolate(xsv_ext2, ysv_ext2, zsv_ext2, wsv_ext2, dx_ext2, dy_ext2, dz_ext2, dw_ext2);
         }

         return value / NORM_CONSTANT_4D;
      }

      private float Extrapolate(int xsb, int ysb, float dx, float dy)
      {
         int index = perm[(perm[xsb & 0xFF] + ysb) & 0xFF] & 0x0E;
         return Gradients2D[index] * dx
            + Gradients2D[index + 1] * dy;
      }

      private float Extrapolate(int xsb, int ysb, int zsb, float dx, float dy, float dz)
      {
         int index = permGradIndex3D[(perm[(perm[xsb & 0xFF] + ysb) & 0xFF] + zsb) & 0xFF];
         return Gradients3D[index] * dx
            + Gradients3D[index + 1] * dy
            + Gradients3D[index + 2] * dz;
      }

      private float Extrapolate(int xsb, int ysb, int zsb, int wsb, float dx, float dy, float dz, float dw)
      {
         int index = perm[(perm[(perm[(perm[xsb & 0xFF] + ysb) & 0xFF] + zsb) & 0xFF] + wsb) & 0xFF] & 0xFC;
         return Gradients4D[index] * dx
            + Gradients4D[index + 1] * dy
            + Gradients4D[index + 2] * dz
            + Gradients4D[index + 3] * dw;
      }

      private static int FastFloor(float x)
      {
         int xi = (int)x;
         return x < xi ? xi - 1 : xi;
      }

      //Gradients for 2D. They approximate the directions to the
      //vertices of an octagon from the center.
      public static readonly int[] Gradients2D = new int[] {
       5,  2,    2,  5,
      -5,  2,   -2,  5,
       5, -2,    2, -5,
      -5, -2,   -2, -5,
   };

      //Gradients for 3D. They approximate the directions to the
      //vertices of a rhombicuboctahedron from the center, skewed so
      //that the triangular and square facets can be inscribed inside
      //circles of the same radius.
      public static readonly int[] Gradients3D = new int[] {
      -11,  4,  4,     -4,  11,  4,    -4,  4,  11,
       11,  4,  4,      4,  11,  4,     4,  4,  11,
      -11, -4,  4,     -4, -11,  4,    -4, -4,  11,
       11, -4,  4,      4, -11,  4,     4, -4,  11,
      -11,  4, -4,     -4,  11, -4,    -4,  4, -11,
       11,  4, -4,      4,  11, -4,     4,  4, -11,
      -11, -4, -4,     -4, -11, -4,    -4, -4, -11,
       11, -4, -4,      4, -11, -4,     4, -4, -11,
   };

      //Gradients for 4D. They approximate the directions to the
      //vertices of a disprismatotesseractihexadecachoron from the center,
      //skewed so that the tetrahedral and cubic facets can be inscribed inside
      //spheres of the same radius.
      public static readonly int[] Gradients4D = new int[] {
        3,  1,  1,  1,      1,  3,  1,  1,      1,  1,  3,  1,      1,  1,  1,  3,
       -3,  1,  1,  1,     -1,  3,  1,  1,     -1,  1,  3,  1,     -1,  1,  1,  3,
        3, -1,  1,  1,      1, -3,  1,  1,      1, -1,  3,  1,      1, -1,  1,  3,
       -3, -1,  1,  1,     -1, -3,  1,  1,     -1, -1,  3,  1,     -1, -1,  1,  3,
        3,  1, -1,  1,      1,  3, -1,  1,      1,  1, -3,  1,      1,  1, -1,  3,
       -3,  1, -1,  1,     -1,  3, -1,  1,     -1,  1, -3,  1,     -1,  1, -1,  3,
        3, -1, -1,  1,      1, -3, -1,  1,      1, -1, -3,  1,      1, -1, -1,  3,
       -3, -1, -1,  1,     -1, -3, -1,  1,     -1, -1, -3,  1,     -1, -1, -1,  3,
        3,  1,  1, -1,      1,  3,  1, -1,      1,  1,  3, -1,      1,  1,  1, -3,
       -3,  1,  1, -1,     -1,  3,  1, -1,     -1,  1,  3, -1,     -1,  1,  1, -3,
        3, -1,  1, -1,      1, -3,  1, -1,      1, -1,  3, -1,      1, -1,  1, -3,
       -3, -1,  1, -1,     -1, -3,  1, -1,     -1, -1,  3, -1,     -1, -1,  1, -3,
        3,  1, -1, -1,      1,  3, -1, -1,      1,  1, -3, -1,      1,  1, -1, -3,
       -3,  1, -1, -1,     -1,  3, -1, -1,     -1,  1, -3, -1,     -1,  1, -1, -3,
        3, -1, -1, -1,      1, -3, -1, -1,      1, -1, -3, -1,      1, -1, -1, -3,
       -3, -1, -1, -1,     -1, -3, -1, -1,     -1, -1, -3, -1,     -1, -1, -1, -3,
   };
   }
}
