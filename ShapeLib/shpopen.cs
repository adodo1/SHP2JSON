/******************************************************************************
 * shpopen.cs
 *
 * Project:  MonoShapelib
 * Purpose:  Implementation of core Shapefile read/write functions.
 * Author:   Ko Nagase, geosanak@gmail.com
 *
 ******************************************************************************
 * Copyright (c) 1999, 2001, Frank Warmerdam
 *
 * This software is available under the following "MIT Style" license,
 * or at the option of the licensee under the LGPL (see LICENSE.LGPL).  This
 * option is discussed in more detail in shapelib.html.
 *
 * --
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included
 * in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
 * OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 ******************************************************************************
 */

#define DISABLE_MULTIPATCH_MEASURE

using System;
using System.IO;

namespace MonoShapelib
{
    public partial class SHPHandle : IDisposable
    {
        #region Fields
        
        private static bool bBigEndian;
        
        #endregion
        
        #region Methods

        public void Dispose()
        {
            Close();
        }

        private static void ByteCopy( byte[] a, byte[] b, int offset, int c )
    
        {
            Buffer.BlockCopy( a, 0, b, offset, c );
        }

        private static void ByteCopy( int[] a, byte[] b, int offset, int c )
        {
            for( int i = 0; i < c/4; i++ )
            {
                ByteCopy( a[i], b, offset + i * 4, 4 );
            }
        }

        private static void ByteCopy( int a, byte[] b, int offset, int c )
        
        {
            ByteCopy( BitConverter.GetBytes( a ), b, offset, c );
        }

        private static void ByteCopy( double a, byte[] b, int offset, int c )
        
        {
            ByteCopy( BitConverter.GetBytes( a ), b, offset, c );
        }
    
        /// <summary>
        /// Swap a 2, 4 or 8 byte word.
        /// </summary>
        /// <param name='length'>Swap length.</param>
        /// <param name='wordP'>Word Pointer.</param>
        /// <param name='offset'>Word Offset.</param>
        private static void SwapWord( int length, byte[] wordP, int offset )
    
        {
            int     i;
            byte    temp;
            
            for( i=0; i < length/2; i++ )
            {
                temp = wordP[offset+i];
                wordP[offset+i] = wordP[offset+length-i-1];
                wordP[offset+length-i-1] = temp;
            }
        }

        private static void SwapWord( int length, ref int intP )
        {
            c.assert( length == 4 );
            byte[]  wordP = BitConverter.GetBytes( intP );
            SwapWord( 4, wordP, 0 );
            intP = BitConverter.ToInt32( wordP, 0 );
        }

        private static void SwapWord( int length, ref double doubleP )
        {
            c.assert( length == 8 );
            byte[] wordP = BitConverter.GetBytes( doubleP );
            SwapWord( 8, wordP, 0 );
            doubleP = BitConverter.ToDouble( wordP, 0 );
        }
    
        /// <summary>
        /// A realloc cover function that will access a NULL pointer as
        /// a valid input.
        /// </summary>
        /// <returns>Reallocated buffer.</returns>
        /// <param name='pMem'>Buffer.</param>
        /// <param name='nNewSize'>New buffer size.</param>
        private static T[] SfRealloc<T>( ref T[] pMem, int nNewSize )
        
        {
            Array.Resize( ref pMem, nNewSize );
            return pMem;
        }
    
        /// <summary>
        /// Write out a header for the .shp and .shx files as well as the
        /// contents of the index (.shx) file.
        /// </summary>
        private void WriteHeader()
        
        {
            SHPHandle   psSHP = this;   /* do not free! */

            byte[]  abyHeader = new byte[100];
            int     i;
            int     i32;
            double  dValue;
            byte[]  panSHX;
        
            /* -------------------------------------------------------------------- */
            /*      Prepare header block for .shp file.                             */
            /* -------------------------------------------------------------------- */
            for( i = 0; i < 100; i++ )
              abyHeader[i] = 0;
        
            abyHeader[2] = 0x27;                /* magic cookie */
            abyHeader[3] = 0x0a;
        
            i32 = psSHP.nFileSize/2;                 /* file size */
            ByteCopy( i32, abyHeader, 24, 4 );
            if( !bBigEndian ) SwapWord( 4, abyHeader, 24 );
            
            i32 = 1000;                     /* version */
            ByteCopy( i32, abyHeader, 28, 4 );
            if( bBigEndian ) SwapWord( 4, abyHeader, 28 );
            
            i32 = (int)psSHP.nShapeType;                  /* shape type */
            ByteCopy( i32, abyHeader, 32, 4 );
            if( bBigEndian ) SwapWord( 4, abyHeader, 32 );
        
            dValue = psSHP.adBoundsMin[0];           /* set bounds */
            ByteCopy( dValue, abyHeader, 36, 8 );
            if( bBigEndian ) SwapWord( 8, abyHeader, 36 );
        
            dValue = psSHP.adBoundsMin[1];
            ByteCopy( dValue, abyHeader, 44, 8 );
            if( bBigEndian ) SwapWord( 8, abyHeader, 44 );
        
            dValue = psSHP.adBoundsMax[0];
            ByteCopy( dValue, abyHeader, 52, 8 );
            if( bBigEndian ) SwapWord( 8, abyHeader, 52 );
        
            dValue = psSHP.adBoundsMax[1];
            ByteCopy( dValue, abyHeader, 60, 8 );
            if( bBigEndian ) SwapWord( 8, abyHeader, 60 );
        
            dValue = psSHP.adBoundsMin[2];           /* z */
            ByteCopy( dValue, abyHeader, 68, 8 );
            if( bBigEndian ) SwapWord( 8, abyHeader, 68 );
        
            dValue = psSHP.adBoundsMax[2];
            ByteCopy( dValue, abyHeader, 76, 8 );
            if( bBigEndian ) SwapWord( 8, abyHeader, 76 );
        
            dValue = psSHP.adBoundsMin[3];           /* m */
            ByteCopy( dValue, abyHeader, 84, 8 );
            if( bBigEndian ) SwapWord( 8, abyHeader, 84 );
        
            dValue = psSHP.adBoundsMax[3];
            ByteCopy( dValue, abyHeader, 92, 8 );
            if( bBigEndian ) SwapWord( 8, abyHeader, 92 );
        
            /* -------------------------------------------------------------------- */
            /*      Write .shp file header.                                         */
            /* -------------------------------------------------------------------- */
            c.fseek( psSHP.fpSHP, 0, 0 );
            c.fwrite( abyHeader, 100, 1, psSHP.fpSHP );
        
            /* -------------------------------------------------------------------- */
            /*      Prepare, and write .shx file header.                            */
            /* -------------------------------------------------------------------- */
            i32 = (psSHP.nRecords * 2 * sizeof(int) + 100)/2;    /* file size */
            ByteCopy( i32, abyHeader, 24, 4 );
            if( !bBigEndian ) SwapWord( 4, abyHeader, 24 );
            
            c.fseek( psSHP.fpSHX, 0, 0 );
            c.fwrite( abyHeader, 100, 1, psSHP.fpSHX );
        
            /* -------------------------------------------------------------------- */
            /*      Write out the .shx contents.                                    */
            /* -------------------------------------------------------------------- */
            panSHX = new byte[sizeof(int) * 2 * psSHP.nRecords];
        
            for( i = 0; i < psSHP.nRecords; i++ )
            {
                ByteCopy( psSHP.panRecOffset[i]/2,
                         panSHX, sizeof(int) * (i*2), 4 );
                ByteCopy( psSHP.panRecSize[i]/2,
                         panSHX, sizeof(int) * (i*2+1), 4 );
                if( !bBigEndian ) SwapWord( 4, panSHX, sizeof(int) * (i*2) );
                if( !bBigEndian ) SwapWord( 4, panSHX, sizeof(int) * (i*2+1) );
            }
        
            c.fwrite( panSHX, sizeof(int) * 2, psSHP.nRecords, psSHP.fpSHX );
        
            c.free( ref panSHX );
        }
    
    
        /// <summary>
        /// Open the .shp and .shx files based on the basename of the
        /// files or either file name.
        /// </summary>
        /// <returns>
        /// SHPHandle instance.
        /// </returns>
        /// <param name='pszLayer'>
        /// The name of the layer to access.  This can be the
        /// name of either the .shp or the .shx file or can
        /// just be the path plus the basename of the pair.
        /// </param>
        /// <param name='pszAccess'>
        /// The fopen() style access string.  At this time only
        /// "rb" (read-only binary) and "rb+" (read/write binary)
        /// should be used.
        /// </param>
        public static SHPHandle Open( string pszLayer, string pszAccess )
        
        {
            string          pszFullname, pszBasename;
            SHPHandle       psSHP;
            
            byte[]      pabyBuf;
            int         i;
            double      dValue;

            /* -------------------------------------------------------------------- */
            /*      Ensure the access string is one of the legal ones.  We          */
            /*      ensure the result string indicates binary to avoid common       */
            /*      problems on Windows.                                            */
            /* -------------------------------------------------------------------- */
            if( c.strcmp(pszAccess,"rb+") == 0 || c.strcmp(pszAccess,"r+b") == 0
                || c.strcmp(pszAccess,"r+") == 0 )
                pszAccess = "r+b";
            else
                pszAccess = "rb";

            /* -------------------------------------------------------------------- */
            /*      Establish the byte order on this machine.                       */
            /* -------------------------------------------------------------------- */
            i = 1;
            if( BitConverter.GetBytes( i )[0] == 1 )
                bBigEndian = false;
            else
                bBigEndian = true;
        
            /* -------------------------------------------------------------------- */
            /*      Initialize the info structure.                                  */
            /* -------------------------------------------------------------------- */
            psSHP = new SHPHandle();
        
            psSHP.bUpdated = false;
        
            /* -------------------------------------------------------------------- */
            /*      Compute the base (layer) name.  If there is any extension       */
            /*      on the passed in filename we will strip it off.                 */
            /* -------------------------------------------------------------------- */
            pszBasename = null;
            c.strcpy( ref pszBasename, pszLayer );
            for( i = c.strlenp(pszBasename)-1; 
             i > 0 && pszBasename[i] != '.' && pszBasename[i] != '/'
                   && pszBasename[i] != '\\';
             i-- ) {}
        
            if( pszBasename[i] == '.' )
                pszBasename = pszBasename.Substring( 0, i );

            /* -------------------------------------------------------------------- */
            /*      Open the .shp and .shx files.  Note that files pulled from      */
            /*      a PC to Unix with upper case filenames won't work!              */
            /* -------------------------------------------------------------------- */
            pszFullname = null;
            c.sprintf( ref pszFullname, "{0}.shp", pszBasename );
            psSHP.fpSHP = c.fopen(pszFullname, pszAccess );
            if( psSHP.fpSHP == null )
            {
                c.sprintf( ref pszFullname, "{0}.SHP", pszBasename );
                psSHP.fpSHP = c.fopen(pszFullname, pszAccess );
            }
            
            if( psSHP.fpSHP == null )
            {
                c.free( ref psSHP );
                c.free( ref pszBasename );
                c.free( ref pszFullname );
                return( null );
            }
        
            c.sprintf( ref pszFullname, "{0}.shx", pszBasename );
            psSHP.fpSHX = c.fopen(pszFullname, pszAccess );
            if( psSHP.fpSHX == null )
            {
                c.sprintf( ref pszFullname, "{0}.SHX", pszBasename );
                psSHP.fpSHX = c.fopen(pszFullname, pszAccess );
            }
            
            if( psSHP.fpSHX == null )
            {
                c.fclose( psSHP.fpSHP );
                c.free( ref psSHP );
                c.free( ref pszBasename );
                c.free( ref pszFullname );
                return( null );
            }
        
            c.free( ref pszFullname );
            c.free( ref pszBasename );
        
            /* -------------------------------------------------------------------- */
            /*      Read the file size from the SHP file.                           */
            /* -------------------------------------------------------------------- */
            pabyBuf = new byte[100];
            c.fread( pabyBuf, 100, 1, psSHP.fpSHP );
        
            psSHP.nFileSize = (pabyBuf[24] * 256 * 256 * 256
                    + pabyBuf[25] * 256 * 256
                    + pabyBuf[26] * 256
                    + pabyBuf[27]) * 2;
        
            /* -------------------------------------------------------------------- */
            /*      Read SHX file Header info                                       */
            /* -------------------------------------------------------------------- */
            c.fread( pabyBuf, 100, 1, psSHP.fpSHX );
        
            if( pabyBuf[0] != 0 
                || pabyBuf[1] != 0 
                || pabyBuf[2] != 0x27 
                || (pabyBuf[3] != 0x0a && pabyBuf[3] != 0x0d) )
            {
                c.fclose( psSHP.fpSHP );
                c.fclose( psSHP.fpSHX );
                c.free( ref psSHP );

                return( null );
            }
        
            psSHP.nRecords = pabyBuf[27] + pabyBuf[26] * 256
              + pabyBuf[25] * 256 * 256 + pabyBuf[24] * 256 * 256 * 256;
            psSHP.nRecords = (psSHP.nRecords*2 - 100) / 8;
        
            psSHP.nShapeType = (SHPT)pabyBuf[32];
        
            if( psSHP.nRecords < 0 || psSHP.nRecords > 256000000 )
            {
                /* this header appears to be corrupt.  Give up. */
                c.fclose( psSHP.fpSHP );
                c.fclose( psSHP.fpSHX );
                c.free( ref psSHP );
                
                return( null );
            }
        
            /* -------------------------------------------------------------------- */
            /*      Read the bounds.                                                */
            /* -------------------------------------------------------------------- */
            dValue = 0;
            if( bBigEndian ) SwapWord( 8, pabyBuf, 36 );
            c.memcpy( ref dValue, pabyBuf, 36, 8 );
            psSHP.adBoundsMin[0] = dValue;
        
            if( bBigEndian ) SwapWord( 8, pabyBuf, 44 );
            c.memcpy( ref dValue, pabyBuf, 44, 8 );
            psSHP.adBoundsMin[1] = dValue;
        
            if( bBigEndian ) SwapWord( 8, pabyBuf, 52 );
            c.memcpy( ref dValue, pabyBuf, 52, 8 );
            psSHP.adBoundsMax[0] = dValue;
        
            if( bBigEndian ) SwapWord( 8, pabyBuf, 60 );
            c.memcpy( ref dValue, pabyBuf, 60, 8 );
            psSHP.adBoundsMax[1] = dValue;
        
            if( bBigEndian ) SwapWord( 8, pabyBuf, 68 );        /* z */
            c.memcpy( ref dValue, pabyBuf, 68, 8 );
            psSHP.adBoundsMin[2] = dValue;
            
            if( bBigEndian ) SwapWord( 8, pabyBuf, 76 );
            c.memcpy( ref dValue, pabyBuf, 76, 8 );
            psSHP.adBoundsMax[2] = dValue;
            
            if( bBigEndian ) SwapWord( 8, pabyBuf, 84 );        /* z */
            c.memcpy( ref dValue, pabyBuf, 84, 8 );
            psSHP.adBoundsMin[3] = dValue;
        
            if( bBigEndian ) SwapWord( 8, pabyBuf, 92 );
            c.memcpy( ref dValue, pabyBuf, 92, 8 );
            psSHP.adBoundsMax[3] = dValue;
        
            c.free( ref pabyBuf );
        
            /* -------------------------------------------------------------------- */
            /*      Read the .shx file to get the offsets to each record in         */
            /*      the .shp file.                                                  */
            /* -------------------------------------------------------------------- */
            psSHP.nMaxRecords = psSHP.nRecords;
        
            psSHP.panRecOffset =
                new int[Math.Max(1,psSHP.nMaxRecords)];
            psSHP.panRecSize =
                new int[Math.Max(1,psSHP.nMaxRecords)];
        
            pabyBuf = new byte[8 * Math.Max(1,psSHP.nRecords)];
            c.fread( pabyBuf, 8, psSHP.nRecords, psSHP.fpSHX );
        
            for( i = 0; i < psSHP.nRecords; i++ )
            {
                int         nOffset, nLength;

                nOffset = 0;
                c.memcpy( ref nOffset, pabyBuf, i * 8, 4 );
                if( !bBigEndian ) SwapWord( 4, ref nOffset );

                nLength = 0;
                c.memcpy( ref nLength, pabyBuf, i * 8 + 4, 4 );
                if( !bBigEndian ) SwapWord( 4, ref nLength );

                psSHP.panRecOffset[i] = nOffset*2;
                psSHP.panRecSize[i] = nLength*2;
            }
            c.free( ref pabyBuf );
        
            return( psSHP );
        }

        /// <summary>
        /// Close the .shp and .shx files.
        /// </summary>
        public void Close()
        
        {
            SHPHandle   psSHP = this;   /* do not free! */

            /* -------------------------------------------------------------------- */
            /*      Update the header if we have modified anything.                 */
            /* -------------------------------------------------------------------- */
            if( psSHP.bUpdated )
            {
                WriteHeader();
            }
        
            /* -------------------------------------------------------------------- */
            /*      Free all resources, and close files.                            */
            /* -------------------------------------------------------------------- */
            c.free( ref psSHP.panRecOffset );
            c.free( ref psSHP.panRecSize );
        
            c.fclose( psSHP.fpSHX );
            c.fclose( psSHP.fpSHP );
        
            if( psSHP.pabyRec != null )
            {
                c.free( ref psSHP.pabyRec );
            }
            
            //free( psSHP );
        }

        /// <summary>
        /// Fetch general information about the shape file.
        /// </summary>
        /// <param name='pnEntities'>
        /// A pointer to an integer into which the number of
        /// entities/structures should be placed.
        /// </param>
        /// <param name='pnShapeType'>
        /// A pointer to a SHPT into which the shapetype
        /// of this file should be placed.  Shapefiles may contain
        /// either SHPT.POINT, SHPT.ARC, SHPT.POLYGON or 
        /// SHPT.MULTIPOINT entities.
        /// </param>
        /// <param name='padfMinBound'>
        /// The X, Y, Z and M minimum values will be placed into
        /// this four entry array.  This may be null.
        /// </param>
        /// <param name='padfMaxBound'>
        /// The X, Y, Z and M maximum values will be placed into
        /// this four entry array.  This may be null.
        /// </param>
        public void GetInfo(out int pnEntities, out SHPT pnShapeType,
                            double[] padfMinBound, double[] padfMaxBound )
        
        {
            SHPHandle   psSHP = this;   /* do not free! */

            int    i;

            pnEntities = psSHP.nRecords;
        
            pnShapeType = psSHP.nShapeType;
        
            for( i = 0; i < 4; i++ )
            {
                if( padfMinBound != null )
                    padfMinBound[i] = psSHP.adBoundsMin[i];
                if( padfMaxBound != null )
                    padfMaxBound[i] = psSHP.adBoundsMax[i];
            }
        }

        /// <summary>
        /// Create a new shape file and return a handle to the open
        /// shape file with read/write access.
        /// </summary>
        /// <returns>
        /// SHPHandle instance.
        /// </returns>
        /// <param name='pszLayer'>
        /// The name of the layer to access.  This can be the
        /// name of either the .shp or the .shx file or can
        /// just be the path plus the basename of the pair.
        /// </param>
        /// <param name='nShapeType'>
        /// The type of shapes to be stored in the newly created
        /// file.  It may be either SHPT.POINT, SHPT.ARC,
        /// SHPT.POLYGON or SHPT.MULTIPOINT.
        /// </param>
        public static SHPHandle Create( string pszLayer, SHPT nShapeType )
        
        {
            string  pszBasename, pszFullname;
            int     i;
            FileStream fpSHP, fpSHX;
            byte[]  abyHeader = new byte[100];
            int     i32;
            double  dValue;
            
            /* -------------------------------------------------------------------- */
            /*      Establish the byte order on this system.                        */
            /* -------------------------------------------------------------------- */
            i = 1;
            if( BitConverter.GetBytes( i )[0] == 1 )
                bBigEndian = false;
            else
                bBigEndian = true;
        
            /* -------------------------------------------------------------------- */
            /*      Compute the base (layer) name.  If there is any extension       */
            /*      on the passed in filename we will strip it off.                 */
            /* -------------------------------------------------------------------- */
            pszBasename = null;
            c.strcpy( ref pszBasename, pszLayer );
            for( i = c.strlenp(pszBasename)-1; 
             i > 0 && pszBasename[i] != '.' && pszBasename[i] != '/'
                   && pszBasename[i] != '\\';
             i-- ) {}
        
            if( pszBasename[i] == '.' )
                pszBasename = pszBasename.Substring( 0, i );

            /* -------------------------------------------------------------------- */
            /*      Open the two files so we can write their headers.               */
            /* -------------------------------------------------------------------- */
            pszFullname = null;
            c.sprintf( ref pszFullname, "{0}.shp", pszBasename );
            fpSHP = c.fopen(pszFullname, "wb" );
            if( fpSHP == null )
                return( null );
        
            c.sprintf( ref pszFullname, "{0}.shx", pszBasename );
            fpSHX = c.fopen(pszFullname, "wb" );
            if( fpSHX == null )
                return( null );
        
            c.free( ref pszFullname );
            c.free( ref pszBasename );
        
            /* -------------------------------------------------------------------- */
            /*      Prepare header block for .shp file.                             */
            /* -------------------------------------------------------------------- */
            for( i = 0; i < 100; i++ )
              abyHeader[i] = 0;
        
            abyHeader[2] = 0x27;                /* magic cookie */
            abyHeader[3] = 0x0a;
        
            i32 = 50;                       /* file size */
            ByteCopy( i32, abyHeader, 24, 4 );
            if( !bBigEndian ) SwapWord( 4, abyHeader, 24 );
            
            i32 = 1000;                     /* version */
            ByteCopy( i32, abyHeader, 28, 4 );
            if( bBigEndian ) SwapWord( 4, abyHeader, 28 );
            
            i32 = (int)nShapeType;          /* shape type */
            ByteCopy( i32, abyHeader, 32, 4 );
            if( bBigEndian ) SwapWord( 4, abyHeader, 32 );
        
            dValue = 0.0;                   /* set bounds */
            ByteCopy( dValue, abyHeader, 36, 8 );
            ByteCopy( dValue, abyHeader, 44, 8 );
            ByteCopy( dValue, abyHeader, 52, 8 );
            ByteCopy( dValue, abyHeader, 60, 8 );
        
            /* -------------------------------------------------------------------- */
            /*      Write .shp file header.                                         */
            /* -------------------------------------------------------------------- */
            c.fwrite( abyHeader, 100, 1, fpSHP );
        
            /* -------------------------------------------------------------------- */
            /*      Prepare, and write .shx file header.                            */
            /* -------------------------------------------------------------------- */
            i32 = 50;                       /* file size */
            ByteCopy( i32, abyHeader, 24, 4 );
            if( !bBigEndian ) SwapWord( 4, abyHeader, 24 );
            
            c.fwrite( abyHeader, 100, 1, fpSHX );
        
            /* -------------------------------------------------------------------- */
            /*      Close the files, and then open them as regular existing files.  */
            /* -------------------------------------------------------------------- */
            c.fclose( fpSHP );
            c.fclose( fpSHX );
        
            return( SHPHandle.Open( pszLayer, "r+b" ) );
        }

        /// <summary>
        /// Compute a bounds rectangle for a shape, and set it into the
        /// indicated location in the record.
        /// </summary>
        private void _SHPSetBounds( byte[] pabyRec, int offset, SHPObject psShape )
        
        {
            ByteCopy( psShape.dfXMin, pabyRec, offset + 0, 8 );
            ByteCopy( psShape.dfYMin, pabyRec, offset + 8, 8 );
            ByteCopy( psShape.dfXMax, pabyRec, offset + 16, 8 );
            ByteCopy( psShape.dfYMax, pabyRec, offset + 24, 8 );
        
            if( bBigEndian )
            {
                SwapWord( 8, pabyRec, offset + 0 );
                SwapWord( 8, pabyRec, offset + 8 );
                SwapWord( 8, pabyRec, offset + 16 );
                SwapWord( 8, pabyRec, offset + 24 );
            }
        }

        /// <summary>
        /// Write out the vertices of a new structure.  Note that it is
        /// only possible to write vertices at the end of the file.
        /// </summary>
        /// <returns>
        /// The entity number of the written shape.
        /// </returns>
        /// <param name='nShapeId'>
        /// The entity number of the shape to write.  A value of
        /// -1 should be used for new shapes.
        /// </param>
        /// <param name='psObject'>
        /// The shape to write to the file. This should have
        /// been created with SHPObject.Create(), or
        /// SHPObject.CreateSimple().
        /// </param>
        public int WriteObject( int nShapeId, SHPObject psObject )
                      
        {
            SHPHandle   psSHP = this;   /* do not free! */

            int     nRecordOffset, nRecordSize = 0;
            byte[]  pabyRec;
            int     i32;
        
            this.bUpdated = true;
        
            /* -------------------------------------------------------------------- */
            /*      Ensure that shape object matches the type of the file it is     */
            /*      being written to.                                               */
            /* -------------------------------------------------------------------- */
            c.assert( psObject.nSHPType == psSHP.nShapeType 
                    || psObject.nSHPType == SHPT.NULL );
        
            /* -------------------------------------------------------------------- */
            /*      Ensure that -1 is used for appends.  Either blow an             */
            /*      assertion, or if they are disabled, set the shapeid to -1       */
            /*      for appends.                                                    */
            /* -------------------------------------------------------------------- */
            c.assert( nShapeId == -1 
                    || (nShapeId >= 0 && nShapeId < psSHP.nRecords) );
        
            if( nShapeId != -1 && nShapeId >= psSHP.nRecords )
                nShapeId = -1;
        
            /* -------------------------------------------------------------------- */
            /*      Add the new entity to the in memory index.                      */
            /* -------------------------------------------------------------------- */
            if( nShapeId == -1 && psSHP.nRecords+1 > psSHP.nMaxRecords )
            {
                psSHP.nMaxRecords = (int)( psSHP.nMaxRecords * 1.3 + 100 );
            
                psSHP.panRecOffset = 
                        SfRealloc( ref psSHP.panRecOffset, psSHP.nMaxRecords );
                psSHP.panRecSize = 
                        SfRealloc( ref psSHP.panRecSize, psSHP.nMaxRecords );
            }
        
            /* -------------------------------------------------------------------- */
            /*      Initialize record.                                              */
            /* -------------------------------------------------------------------- */
            pabyRec = new byte[psObject.nVertices * 4 * sizeof(double) 
                           + psObject.nParts * 8 + 128];
            
            /* -------------------------------------------------------------------- */
            /*      Extract vertices for a Polygon or Arc.                          */
            /* -------------------------------------------------------------------- */
            if( psObject.nSHPType == SHPT.POLYGON
                || psObject.nSHPType == SHPT.POLYGONZ
                || psObject.nSHPType == SHPT.POLYGONM
                || psObject.nSHPType == SHPT.ARC
                || psObject.nSHPType == SHPT.ARCZ
                || psObject.nSHPType == SHPT.ARCM
                || psObject.nSHPType == SHPT.MULTIPATCH )
            {
                int         nPoints, nParts;
                int         i;
            
                nPoints = psObject.nVertices;
                nParts = psObject.nParts;
            
                _SHPSetBounds( pabyRec, 12, psObject );
            
                if( bBigEndian ) SwapWord( 4, ref nPoints );
                if( bBigEndian ) SwapWord( 4, ref nParts );
            
                ByteCopy( nPoints, pabyRec, 40 + 8, 4 );
                ByteCopy( nParts, pabyRec, 36 + 8, 4 );
            
                nRecordSize = 52;
            
                /*
                 * Write part start positions.
                 */
                ByteCopy( psObject.panPartStart, pabyRec, 44 + 8,
                              4 * psObject.nParts );
                for( i = 0; i < psObject.nParts; i++ )
                {
                    if( bBigEndian ) SwapWord( 4, pabyRec, 44 + 8 + 4*i );
                        nRecordSize += 4;
                }
        
                /*
                 * Write multipatch part types if needed.
                 */
                if( psObject.nSHPType == SHPT.MULTIPATCH )
                {
                    c.memcpy( pabyRec, nRecordSize, psObject.panPartType,
                            4*psObject.nParts );
                    for( i = 0; i < psObject.nParts; i++ )
                    {
                        if( bBigEndian ) SwapWord( 4, pabyRec, nRecordSize );
                        nRecordSize += 4;
                    }
                }
        
                /*
                 * Write the (x,y) vertex values.
                 */
                for( i = 0; i < psObject.nVertices; i++ )
                {
                    ByteCopy( psObject.padfX[i], pabyRec, nRecordSize, 8 );
                    ByteCopy( psObject.padfY[i], pabyRec, nRecordSize + 8, 8 );
            
                    if( bBigEndian )
                        SwapWord( 8, pabyRec, nRecordSize );
                        
                    if( bBigEndian )
                        SwapWord( 8, pabyRec, nRecordSize + 8 );
            
                    nRecordSize += 2 * 8;
                }
        
                /*
                 * Write the Z coordinates (if any).
                 */
                if( psObject.nSHPType == SHPT.POLYGONZ
                    || psObject.nSHPType == SHPT.ARCZ
                    || psObject.nSHPType == SHPT.MULTIPATCH )
                {
                    ByteCopy( psObject.dfZMin, pabyRec, nRecordSize, 8 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                    nRecordSize += 8;
                    
                    ByteCopy( psObject.dfZMax, pabyRec, nRecordSize, 8 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                    nRecordSize += 8;
        
                    for( i = 0; i < psObject.nVertices; i++ )
                    {
                        ByteCopy( psObject.padfZ[i], pabyRec, nRecordSize, 8 );
                        if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                        nRecordSize += 8;
                    }
                }
        
                /*
                 * Write the M values, if any.
                 */
                if( psObject.nSHPType == SHPT.POLYGONM
                    || psObject.nSHPType == SHPT.ARCM
#if !DISABLE_MULTIPATCH_MEASURE            
                    || psObject.nSHPType == SHPT.MULTIPATCH
#endif            
                    || psObject.nSHPType == SHPT.POLYGONZ
                    || psObject.nSHPType == SHPT.ARCZ )
                {
                    ByteCopy( psObject.dfMMin, pabyRec, nRecordSize, 8 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                    nRecordSize += 8;
                    
                    ByteCopy( psObject.dfMMax, pabyRec, nRecordSize, 8 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                    nRecordSize += 8;
        
                    for( i = 0; i < psObject.nVertices; i++ )
                    {
                        ByteCopy( psObject.padfM[i], pabyRec, nRecordSize, 8 );
                        if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                        nRecordSize += 8;
                    }
                }
            }
        
            /* -------------------------------------------------------------------- */
            /*      Extract vertices for a MultiPoint.                              */
            /* -------------------------------------------------------------------- */
            else if( psObject.nSHPType == SHPT.MULTIPOINT
                     || psObject.nSHPType == SHPT.MULTIPOINTZ
                     || psObject.nSHPType == SHPT.MULTIPOINTM )
            {
                int         nPoints;
                int         i;
            
                nPoints = psObject.nVertices;
        
                _SHPSetBounds( pabyRec, 12, psObject );
        
                if( bBigEndian ) SwapWord( 4, ref nPoints );
                ByteCopy( nPoints, pabyRec, 44, 4 );
                
                for( i = 0; i < psObject.nVertices; i++ )
                {
                    ByteCopy( psObject.padfX[i], pabyRec, 48 + i*16, 8 );
                    ByteCopy( psObject.padfY[i], pabyRec, 48 + i*16 + 8, 8 );
            
                    if( bBigEndian ) SwapWord( 8, pabyRec, 48 + i*16 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, 48 + i*16 + 8 );
                }
            
                nRecordSize = 48 + 16 * psObject.nVertices;
        
                if( psObject.nSHPType == SHPT.MULTIPOINTZ )
                {
                    ByteCopy( psObject.dfZMin, pabyRec, nRecordSize, 8 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                    nRecordSize += 8;
        
                    ByteCopy( psObject.dfZMax, pabyRec, nRecordSize, 8 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                    nRecordSize += 8;
                    
                    for( i = 0; i < psObject.nVertices; i++ )
                    {
                        ByteCopy( psObject.padfZ[i], pabyRec, nRecordSize, 8 );
                        if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                        nRecordSize += 8;
                    }
                }
        
                if( psObject.nSHPType == SHPT.MULTIPOINTZ
                    || psObject.nSHPType == SHPT.MULTIPOINTM )
                {
                    ByteCopy( psObject.dfMMin, pabyRec, nRecordSize, 8 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                    nRecordSize += 8;
        
                    ByteCopy( psObject.dfMMax, pabyRec, nRecordSize, 8 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                    nRecordSize += 8;
                    
                    for( i = 0; i < psObject.nVertices; i++ )
                    {
                        ByteCopy( psObject.padfM[i], pabyRec, nRecordSize, 8 );
                        if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                        nRecordSize += 8;
                    }
                }
            }
        
            /* -------------------------------------------------------------------- */
            /*      Write point.                                                    */
            /* -------------------------------------------------------------------- */
            else if( psObject.nSHPType == SHPT.POINT
                     || psObject.nSHPType == SHPT.POINTZ
                     || psObject.nSHPType == SHPT.POINTM )
            {
                ByteCopy( psObject.padfX[0], pabyRec, 12, 8 );
                ByteCopy( psObject.padfY[0], pabyRec, 20, 8 );
            
                if( bBigEndian ) SwapWord( 8, pabyRec, 12 );
                if( bBigEndian ) SwapWord( 8, pabyRec, 20 );
        
                nRecordSize = 28;
                
                if( psObject.nSHPType == SHPT.POINTZ )
                {
                    ByteCopy( psObject.padfZ[0], pabyRec, nRecordSize, 8 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                    nRecordSize += 8;
                }
                
                if( psObject.nSHPType == SHPT.POINTZ
                    || psObject.nSHPType == SHPT.POINTM )
                {
                    ByteCopy( psObject.padfM[0], pabyRec, nRecordSize, 8 );
                    if( bBigEndian ) SwapWord( 8, pabyRec, nRecordSize );
                    nRecordSize += 8;
                }
            }
        
            /* -------------------------------------------------------------------- */
            /*      Not much to do for null geometries.                             */
            /* -------------------------------------------------------------------- */
            else if( psObject.nSHPType == SHPT.NULL )
            {
                nRecordSize = 12;
            }
        
            else
            {
                /* unknown type */
                c.assert( false );
            }
        
            /* -------------------------------------------------------------------- */
            /*      Establish where we are going to put this record. If we are      */
            /*      rewriting and existing record, and it will fit, then put it     */
            /*      back where the original came from.  Otherwise write at the end. */
            /* -------------------------------------------------------------------- */
            if( nShapeId == -1 || psSHP.panRecSize[nShapeId] < nRecordSize-8 )
            {
                if( nShapeId == -1 )
                    nShapeId = psSHP.nRecords++;
        
                psSHP.panRecOffset[nShapeId] = nRecordOffset = psSHP.nFileSize;
                psSHP.panRecSize[nShapeId] = nRecordSize-8;
                psSHP.nFileSize += nRecordSize;
            }
            else
            {
                nRecordOffset = psSHP.panRecOffset[nShapeId];
            }
            
            /* -------------------------------------------------------------------- */
            /*      Set the shape type, record number, and record size.             */
            /* -------------------------------------------------------------------- */
            i32 = nShapeId+1;                   /* record # */
            if( !bBigEndian ) SwapWord( 4, ref i32 );
            ByteCopy( i32, pabyRec, 0, 4 );
        
            i32 = (nRecordSize-8)/2;                /* record size */
            if( !bBigEndian ) SwapWord( 4, ref i32 );
            ByteCopy( i32, pabyRec, 4, 4 );
        
            i32 = (int)psObject.nSHPType;           /* shape type */
            if( bBigEndian ) SwapWord( 4, ref i32 );
            ByteCopy( i32, pabyRec, 8, 4 );
        
            /* -------------------------------------------------------------------- */
            /*      Write out record.                                               */
            /* -------------------------------------------------------------------- */
            if( c.fseek( psSHP.fpSHP, nRecordOffset, 0 ) != 0
                || c.fwrite( pabyRec, nRecordSize, 1, psSHP.fpSHP ) < 1 )
            {
                c.printf( "Error in fseek() or fwrite().\n" );
                c.free( ref pabyRec );
                return -1;
            }
            
            c.free( ref pabyRec );
        
            /* -------------------------------------------------------------------- */
            /*  Expand file wide bounds based on this shape.                        */
            /* -------------------------------------------------------------------- */
            if( psSHP.adBoundsMin[0] == 0.0
                && psSHP.adBoundsMax[0] == 0.0
                && psSHP.adBoundsMin[1] == 0.0
                && psSHP.adBoundsMax[1] == 0.0 
                && psObject.nSHPType != SHPT.NULL )
            {
                psSHP.adBoundsMin[0] = psSHP.adBoundsMax[0] = psObject.padfX[0];
                psSHP.adBoundsMin[1] = psSHP.adBoundsMax[1] = psObject.padfY[0];
                psSHP.adBoundsMin[2] = psSHP.adBoundsMax[2] = psObject.padfZ[0];
                psSHP.adBoundsMin[3] = psSHP.adBoundsMax[3] = psObject.padfM[0];
            }
        
            for( int i = 0; i < psObject.nVertices; i++ )
            {
                psSHP.adBoundsMin[0] = Math.Min(psSHP.adBoundsMin[0],psObject.padfX[i]);
                psSHP.adBoundsMin[1] = Math.Min(psSHP.adBoundsMin[1],psObject.padfY[i]);
                psSHP.adBoundsMin[2] = Math.Min(psSHP.adBoundsMin[2],psObject.padfZ[i]);
                psSHP.adBoundsMin[3] = Math.Min(psSHP.adBoundsMin[3],psObject.padfM[i]);
                psSHP.adBoundsMax[0] = Math.Max(psSHP.adBoundsMax[0],psObject.padfX[i]);
                psSHP.adBoundsMax[1] = Math.Max(psSHP.adBoundsMax[1],psObject.padfY[i]);
                psSHP.adBoundsMax[2] = Math.Max(psSHP.adBoundsMax[2],psObject.padfZ[i]);
                psSHP.adBoundsMax[3] = Math.Max(psSHP.adBoundsMax[3],psObject.padfM[i]);
            }
        
            return( nShapeId  );
        }

        /// <summary>
        /// Read the vertices, parts, and other non-attribute information
        /// for one shape.
        /// </summary>
        /// <returns>
        /// SHPObject instance.
        /// </returns>
        /// <param name='hEntity'>
        /// The entity number of the shape to read.  Entity
        /// numbers are between 0 and nEntities-1 (as returned
        /// by GetInfo()).
        /// </param>
        public SHPObject ReadObject( int hEntity )
        
        {
            SHPHandle   psSHP = this;   /* do not free! */

            SHPObject       psShape;
        
            /* -------------------------------------------------------------------- */
            /*      Validate the record/entity number.                              */
            /* -------------------------------------------------------------------- */
            if( hEntity < 0 || hEntity >= psSHP.nRecords )
                return( null );
        
            /* -------------------------------------------------------------------- */
            /*      Ensure our record buffer is large enough.                       */
            /* -------------------------------------------------------------------- */
            if( psSHP.panRecSize[hEntity]+8 > psSHP.nBufSize )
            {
                psSHP.nBufSize = psSHP.panRecSize[hEntity]+8;
                psSHP.pabyRec = SfRealloc(ref psSHP.pabyRec,psSHP.nBufSize);
            }
        
            /* -------------------------------------------------------------------- */
            /*      Read the record.                                                */
            /* -------------------------------------------------------------------- */
            c.fseek( psSHP.fpSHP, psSHP.panRecOffset[hEntity], 0 );
            c.fread( psSHP.pabyRec, psSHP.panRecSize[hEntity]+8, 1, psSHP.fpSHP );

            /* -------------------------------------------------------------------- */
            /*      Allocate and minimally initialize the object.                   */
            /* -------------------------------------------------------------------- */
            psShape = new SHPObject();
            psShape.nShapeId = hEntity;
        
            int _nSHPType = 0;
            c.memcpy( ref _nSHPType, this.pabyRec, 8, 4 );
            if( bBigEndian ) SwapWord( 4, ref _nSHPType );
            psShape.nSHPType = (SHPT)_nSHPType;
        
            /* ==================================================================== */
            /*      Extract vertices for a Polygon or Arc.                          */
            /* ==================================================================== */
            if( psShape.nSHPType == SHPT.POLYGON || psShape.nSHPType == SHPT.ARC
                || psShape.nSHPType == SHPT.POLYGONZ
                || psShape.nSHPType == SHPT.POLYGONM
                || psShape.nSHPType == SHPT.ARCZ
                || psShape.nSHPType == SHPT.ARCM
                || psShape.nSHPType == SHPT.MULTIPATCH )
            {
                int         nPoints, nParts;
                int         i, nOffset;
        
                /* -------------------------------------------------------------------- */
                /*      Get the X/Y bounds.                                             */
                /* -------------------------------------------------------------------- */
                c.memcpy( ref psShape.dfXMin, psSHP.pabyRec, 8 +  4, 8 );
                c.memcpy( ref psShape.dfYMin, psSHP.pabyRec, 8 + 12, 8 );
                c.memcpy( ref psShape.dfXMax, psSHP.pabyRec, 8 + 20, 8 );
                c.memcpy( ref psShape.dfYMax, psSHP.pabyRec, 8 + 28, 8 );
        
                if( bBigEndian ) SwapWord( 8, ref psShape.dfXMin );
                if( bBigEndian ) SwapWord( 8, ref psShape.dfYMin );
                if( bBigEndian ) SwapWord( 8, ref psShape.dfXMax );
                if( bBigEndian ) SwapWord( 8, ref psShape.dfYMax );
        
                /* -------------------------------------------------------------------- */
                /*      Extract part/point count, and build vertex and part arrays      */
                /*      to proper size.                                                 */
                /* -------------------------------------------------------------------- */
                nPoints = 0; nParts = 0;
                c.memcpy( ref nPoints, psSHP.pabyRec, 40 + 8, 4 );
                c.memcpy( ref nParts, psSHP.pabyRec, 36 + 8, 4 );
            
                if( bBigEndian ) SwapWord( 4, ref nPoints );
                if( bBigEndian ) SwapWord( 4, ref nParts );
            
                psShape.nVertices = nPoints;
                psShape.padfX = new double[nPoints];
                psShape.padfY = new double[nPoints];
                psShape.padfZ = new double[nPoints];
                psShape.padfM = new double[nPoints];
            
                psShape.nParts = nParts;
                psShape.panPartStart = new int[nParts];
                psShape.panPartType = new int[nParts];
        
                for( i = 0; i < nParts; i++ )
                    psShape.panPartType[i] = (int)SHPP.RING;
        
                /* -------------------------------------------------------------------- */
                /*      Copy out the part array from the record.                        */
                /* -------------------------------------------------------------------- */
                c.memcpy( psShape.panPartStart, psSHP.pabyRec, 44 + 8, 4 * nParts );
                for( i = 0; i < nParts; i++ )
                {
                    if( bBigEndian ) SwapWord( 4, ref psShape.panPartStart[i] );
                }
            
                nOffset = 44 + 8 + 4*nParts;
            
                /* -------------------------------------------------------------------- */
                /*      If this is a multipatch, we will also have parts types.         */
                /* -------------------------------------------------------------------- */
                if( psShape.nSHPType == SHPT.MULTIPATCH )
                {
                    c.memcpy( psShape.panPartType, psSHP.pabyRec, nOffset, 4*nParts );
                    for( i = 0; i < nParts; i++ )
                    {
                        if( bBigEndian ) SwapWord( 4, ref psShape.panPartType[i] );
                    }
        
                    nOffset += 4*nParts;
                }
                
                /* -------------------------------------------------------------------- */
                /*      Copy out the vertices from the record.                          */
                /* -------------------------------------------------------------------- */
                for( i = 0; i < nPoints; i++ )
                {
                    c.memcpy(ref psShape.padfX[i],
                       psSHP.pabyRec, nOffset + i * 16,
                       8 );
            
                    c.memcpy(ref psShape.padfY[i],
                       psSHP.pabyRec, nOffset + i * 16 + 8,
                       8 );
            
                    if( bBigEndian ) SwapWord( 8, ref psShape.padfX[i] );
                    if( bBigEndian ) SwapWord( 8, ref psShape.padfY[i] );
                }
        
                nOffset += 16*nPoints;
                
                /* -------------------------------------------------------------------- */
                /*      If we have a Z coordinate, collect that now.                    */
                /* -------------------------------------------------------------------- */
                if( psShape.nSHPType == SHPT.POLYGONZ
                    || psShape.nSHPType == SHPT.ARCZ
                    || psShape.nSHPType == SHPT.MULTIPATCH )
                {
                    c.memcpy( ref psShape.dfZMin, psSHP.pabyRec, nOffset, 8 );
                    c.memcpy( ref psShape.dfZMax, psSHP.pabyRec, nOffset + 8, 8 );
                    
                    if( bBigEndian ) SwapWord( 8, ref psShape.dfZMin );
                    if( bBigEndian ) SwapWord( 8, ref psShape.dfZMax );
                    
                    for( i = 0; i < nPoints; i++ )
                    {
                        c.memcpy( ref psShape.padfZ[i],
                                psSHP.pabyRec, nOffset + 16 + i*8, 8 );
                        if( bBigEndian ) SwapWord( 8, ref psShape.padfZ[i] );
                    }
        
                    nOffset += 16 + 8*nPoints;
                }
        
                /* -------------------------------------------------------------------- */
                /*      If we have a M measure value, then read it now.  We assume      */
                /*      that the measure can be present for any shape if the size is    */
                /*      big enough, but really it will only occur for the Z shapes      */
                /*      (options), and the M shapes.                                    */
                /* -------------------------------------------------------------------- */
                if( this.panRecSize[hEntity]+8 >= nOffset + 16 + 8*nPoints )
                {
                    c.memcpy( ref psShape.dfMMin, psSHP.pabyRec, nOffset, 8 );
                    c.memcpy( ref psShape.dfMMax, psSHP.pabyRec, nOffset + 8, 8 );
                    
                    if( bBigEndian ) SwapWord( 8, ref psShape.dfMMin );
                    if( bBigEndian ) SwapWord( 8, ref psShape.dfMMax );
                    
                    for( i = 0; i < nPoints; i++ )
                    {
                        c.memcpy( ref psShape.padfM[i],
                                psSHP.pabyRec, nOffset + 16 + i*8, 8 );
                        if( bBigEndian ) SwapWord( 8, ref psShape.padfM[i] );
                    }
                }
                
            }
        
            /* ==================================================================== */
            /*      Extract vertices for a MultiPoint.                              */
            /* ==================================================================== */
            else if( psShape.nSHPType == SHPT.MULTIPOINT
                     || psShape.nSHPType == SHPT.MULTIPOINTM
                     || psShape.nSHPType == SHPT.MULTIPOINTZ )
            {
                int         nPoints;
                int         i, nOffset;
            
                nPoints = 0;
                c.memcpy( ref nPoints, psSHP.pabyRec, 44, 4 );
                if( bBigEndian ) SwapWord( 4, ref nPoints );
            
                psShape.nVertices = nPoints;
                psShape.padfX = new double[nPoints];
                psShape.padfY = new double[nPoints];
                psShape.padfZ = new double[nPoints];
                psShape.padfM = new double[nPoints];
        
                for( i = 0; i < nPoints; i++ )
                {
                    c.memcpy(ref psShape.padfX[i], psSHP.pabyRec, 48 + 16 * i, 8 );
                    c.memcpy(ref psShape.padfY[i], psSHP.pabyRec, 48 + 16 * i + 8, 8 );
            
                    if( bBigEndian ) SwapWord( 8, ref psShape.padfX[i] );
                    if( bBigEndian ) SwapWord( 8, ref psShape.padfY[i] );
                }
        
                nOffset = 48 + 16*nPoints;
                
                /* -------------------------------------------------------------------- */
                /*      Get the X/Y bounds.                                             */
                /* -------------------------------------------------------------------- */
                c.memcpy( ref psShape.dfXMin, psSHP.pabyRec, 8 +  4, 8 );
                c.memcpy( ref psShape.dfYMin, psSHP.pabyRec, 8 + 12, 8 );
                c.memcpy( ref psShape.dfXMax, psSHP.pabyRec, 8 + 20, 8 );
                c.memcpy( ref psShape.dfYMax, psSHP.pabyRec, 8 + 28, 8 );
        
                if( bBigEndian ) SwapWord( 8, ref psShape.dfXMin );
                if( bBigEndian ) SwapWord( 8, ref psShape.dfYMin );
                if( bBigEndian ) SwapWord( 8, ref psShape.dfXMax );
                if( bBigEndian ) SwapWord( 8, ref psShape.dfYMax );
        
                /* -------------------------------------------------------------------- */
                /*      If we have a Z coordinate, collect that now.                    */
                /* -------------------------------------------------------------------- */
                if( psShape.nSHPType == SHPT.MULTIPOINTZ )
                {
                    c.memcpy( ref psShape.dfZMin, psSHP.pabyRec, nOffset, 8 );
                    c.memcpy( ref psShape.dfZMax, psSHP.pabyRec, nOffset + 8, 8 );
                    
                    if( bBigEndian ) SwapWord( 8, ref psShape.dfZMin );
                    if( bBigEndian ) SwapWord( 8, ref psShape.dfZMax );
                    
                    for( i = 0; i < nPoints; i++ )
                    {
                        c.memcpy( ref psShape.padfZ[i],
                                psSHP.pabyRec, nOffset + 16 + i*8, 8 );
                        if( bBigEndian ) SwapWord( 8, ref psShape.padfZ[i] );
                    }
        
                    nOffset += 16 + 8*nPoints;
                }
        
                /* -------------------------------------------------------------------- */
                /*      If we have a M measure value, then read it now.  We assume      */
                /*      that the measure can be present for any shape if the size is    */
                /*      big enough, but really it will only occur for the Z shapes      */
                /*      (options), and the M shapes.                                    */
                /* -------------------------------------------------------------------- */
                if( psSHP.panRecSize[hEntity]+8 >= nOffset + 16 + 8*nPoints )
                {
                    c.memcpy( ref psShape.dfMMin, psSHP.pabyRec, nOffset, 8 );
                    c.memcpy( ref psShape.dfMMax, psSHP.pabyRec, nOffset + 8, 8 );
                    
                    if( bBigEndian ) SwapWord( 8, ref psShape.dfMMin );
                    if( bBigEndian ) SwapWord( 8, ref psShape.dfMMax );
                    
                    for( i = 0; i < nPoints; i++ )
                    {
                        c.memcpy( ref psShape.padfM[i],
                                psSHP.pabyRec, nOffset + 16 + i*8, 8 );
                        if( bBigEndian ) SwapWord( 8, ref psShape.padfM[i] );
                    }
                }
            }
        
            /* ==================================================================== */
            /*      Extract vertices for a point.                                   */
            /* ==================================================================== */
            else if( psShape.nSHPType == SHPT.POINT
                     || psShape.nSHPType == SHPT.POINTM
                     || psShape.nSHPType == SHPT.POINTZ )
            {
                int nOffset;
                
                psShape.nVertices = 1;
                psShape.padfX = new double[1];
                psShape.padfY = new double[1];
                psShape.padfZ = new double[1];
                psShape.padfM = new double[1];
        
                c.memcpy( ref psShape.padfX[0], this.pabyRec, 12, 8 );
                c.memcpy( ref psShape.padfY[0], this.pabyRec, 20, 8 );
            
                if( bBigEndian ) SwapWord( 8, ref psShape.padfX[0] );
                if( bBigEndian ) SwapWord( 8, ref psShape.padfY[0] );
        
                nOffset = 20 + 8;
                
                /* -------------------------------------------------------------------- */
                /*      If we have a Z coordinate, collect that now.                    */
                /* -------------------------------------------------------------------- */
                if( psShape.nSHPType == SHPT.POINTZ )
                {
                    c.memcpy( ref psShape.padfZ[0], psSHP.pabyRec, nOffset, 8 );
                
                    if( bBigEndian ) SwapWord( 8, ref psShape.padfZ[0] );
                    
                    nOffset += 8;
                }
        
                /* -------------------------------------------------------------------- */
                /*      If we have a M measure value, then read it now.  We assume      */
                /*      that the measure can be present for any shape if the size is    */
                /*      big enough, but really it will only occur for the Z shapes      */
                /*      (options), and the M shapes.                                    */
                /* -------------------------------------------------------------------- */
                if( this.panRecSize[hEntity]+8 >= nOffset + 8 )
                {
                    c.memcpy( ref psShape.padfM[0], psSHP.pabyRec, nOffset, 8 );
                
                    if( bBigEndian ) SwapWord( 8, ref psShape.padfM[0] );
                }
        
                /* -------------------------------------------------------------------- */
                /*      Since no extents are supplied in the record, we will apply      */
                /*      them from the single vertex.                                    */
                /* -------------------------------------------------------------------- */
                psShape.dfXMin = psShape.dfXMax = psShape.padfX[0];
                psShape.dfYMin = psShape.dfYMax = psShape.padfY[0];
                psShape.dfZMin = psShape.dfZMax = psShape.padfZ[0];
                psShape.dfMMin = psShape.dfMMax = psShape.padfM[0];
            }
        
            return( psShape );
        }

        /// <summary>
        /// Reset the winding of polygon objects to adhere to the
        /// specification.
        /// </summary>
        /// <returns>
        /// 0< if a change is made and 0 if no change is made.
        /// </returns>
        /// <param name='psObject'>
        /// The object to deallocate.
        /// </param>
        public int RewindObject( SHPObject psObject )
        
        {
            int iOpRing, bAltered = 0;
        
            /* -------------------------------------------------------------------- */
            /*      Do nothing if this is not a polygon object.                     */
            /* -------------------------------------------------------------------- */
            if( psObject.nSHPType != SHPT.POLYGON
                && psObject.nSHPType != SHPT.POLYGONZ
                && psObject.nSHPType != SHPT.POLYGONM )
                return 0;
        
            /* -------------------------------------------------------------------- */
            /*      Process each of the rings.                                      */
            /* -------------------------------------------------------------------- */
            for( iOpRing = 0; iOpRing < psObject.nParts; iOpRing++ )
            {
                bool    bInner;
                int     iVert, nVertCount, nVertStart, iCheckRing;
                double  dfSum, dfTestX, dfTestY;
        
                /* -------------------------------------------------------------------- */
                /*      Determine if this ring is an inner ring or an outer ring        */
                /*      relative to all the other rings.  For now we assume the         */
                /*      first ring is outer and all others are inner, but eventually    */
                /*      we need to fix this to handle multiple island polygons and      */
                /*      unordered sets of rings.                                        */
                /* -------------------------------------------------------------------- */
                dfTestX = psObject.padfX[psObject.panPartStart[iOpRing]];
                dfTestY = psObject.padfY[psObject.panPartStart[iOpRing]];
        
                bInner = false;
                for( iCheckRing = 0; iCheckRing < psObject.nParts; iCheckRing++ )
                {
                    int iEdge;
        
                    if( iCheckRing == iOpRing )
                        continue;
                    
                    nVertStart = psObject.panPartStart[iCheckRing];
        
                    if( iCheckRing == psObject.nParts-1 )
                        nVertCount = psObject.nVertices 
                            - psObject.panPartStart[iCheckRing];
                    else
                        nVertCount = psObject.panPartStart[iCheckRing+1] 
                            - psObject.panPartStart[iCheckRing];
        
                    for( iEdge = 0; iEdge < nVertCount; iEdge++ )
                    {
                        int iNext;
        
                        if( iEdge < nVertCount-1 )
                            iNext = iEdge+1;
                        else
                            iNext = 0;
        
                        if( (psObject.padfY[iEdge+nVertStart] < dfTestY 
                             && psObject.padfY[iNext+nVertStart] >= dfTestY)
                            || (psObject.padfY[iNext+nVertStart] < dfTestY 
                                && psObject.padfY[iEdge+nVertStart] >= dfTestY) )
                        {
                            if( psObject.padfX[iEdge+nVertStart] 
                                + (dfTestY - psObject.padfY[iEdge+nVertStart])
                                   / (psObject.padfY[iNext+nVertStart]
                                      - psObject.padfY[iEdge+nVertStart])
                                   * (psObject.padfX[iNext+nVertStart]
                                      - psObject.padfX[iEdge+nVertStart]) < dfTestX )
                                bInner = !bInner;
                        }
                    }
                }
        
                /* -------------------------------------------------------------------- */
                /*      Determine the current order of this ring so we will know if     */
                /*      it has to be reversed.                                          */
                /* -------------------------------------------------------------------- */
                nVertStart = psObject.panPartStart[iOpRing];
        
                if( iOpRing == psObject.nParts-1 )
                    nVertCount = psObject.nVertices - psObject.panPartStart[iOpRing];
                else
                    nVertCount = psObject.panPartStart[iOpRing+1] 
                        - psObject.panPartStart[iOpRing];
        
                dfSum = 0.0;
                for( iVert = nVertStart; iVert < nVertStart+nVertCount-1; iVert++ )
                {
                    dfSum += psObject.padfX[iVert] * psObject.padfY[iVert+1]
                        - psObject.padfY[iVert] * psObject.padfX[iVert+1];
                }
        
                dfSum += psObject.padfX[iVert] * psObject.padfY[nVertStart]
                       - psObject.padfY[iVert] * psObject.padfX[nVertStart];
        
                /* -------------------------------------------------------------------- */
                /*      Reverse if necessary.                                           */
                /* -------------------------------------------------------------------- */
                if( (dfSum < 0.0 && bInner) || (dfSum > 0.0 && !bInner) )
                {
                    int   i;
        
                    bAltered++;
                    for( i = 0; i < nVertCount/2; i++ )
                    {
                        double dfSaved;
        
                        /* Swap X */
                        dfSaved = psObject.padfX[nVertStart+i];
                        psObject.padfX[nVertStart+i] = 
                            psObject.padfX[nVertStart+nVertCount-i-1];
                        psObject.padfX[nVertStart+nVertCount-i-1] = dfSaved;
        
                        /* Swap Y */
                        dfSaved = psObject.padfY[nVertStart+i];
                        psObject.padfY[nVertStart+i] = 
                            psObject.padfY[nVertStart+nVertCount-i-1];
                        psObject.padfY[nVertStart+nVertCount-i-1] = dfSaved;
        
                        /* Swap Z */
                        if( psObject.padfZ != null )
                        {
                            dfSaved = psObject.padfZ[nVertStart+i];
                            psObject.padfZ[nVertStart+i] = 
                                psObject.padfZ[nVertStart+nVertCount-i-1];
                            psObject.padfZ[nVertStart+nVertCount-i-1] = dfSaved;
                        }
        
                        /* Swap M */
                        if( psObject.padfM != null )
                        {
                            dfSaved = psObject.padfM[nVertStart+i];
                            psObject.padfM[nVertStart+i] = 
                                psObject.padfM[nVertStart+nVertCount-i-1];
                            psObject.padfM[nVertStart+nVertCount-i-1] = dfSaved;
                        }
                    }
                }
            }
        
            return bAltered;
        }

        #endregion
    }

    public partial class SHPObject
    {
        /// <summary>
        /// Recompute the extents of a shape.  Automatically done by
        /// SHPObject.Create().
        /// </summary>
        public void ComputeExtents()
        
        {
            int     i;
            
            /* -------------------------------------------------------------------- */
            /*      Build extents for this object.                                  */
            /* -------------------------------------------------------------------- */
            if( this.nVertices > 0 )
            {
                this.dfXMin = this.dfXMax = this.padfX[0];
                this.dfYMin = this.dfYMax = this.padfY[0];
                this.dfZMin = this.dfZMax = this.padfZ[0];
                this.dfMMin = this.dfMMax = this.padfM[0];
            }
            
            for( i = 0; i < this.nVertices; i++ )
            {
                this.dfXMin = Math.Min(this.dfXMin, this.padfX[i]);
                this.dfYMin = Math.Min(this.dfYMin, this.padfY[i]);
                this.dfZMin = Math.Min(this.dfZMin, this.padfZ[i]);
                this.dfMMin = Math.Min(this.dfMMin, this.padfM[i]);
        
                this.dfXMax = Math.Max(this.dfXMax, this.padfX[i]);
                this.dfYMax = Math.Max(this.dfYMax, this.padfY[i]);
                this.dfZMax = Math.Max(this.dfZMax, this.padfZ[i]);
                this.dfMMax = Math.Max(this.dfMMax, this.padfM[i]);
            }
        }

        /// <summary>
        /// Create a shape object.  It should be freed with
        /// SHPObject.Destroy().
        /// and padfM.
        /// </summary>
        /// <returns>
        /// SHPObject instance.
        /// </returns>
        /// <param name='nSHPType'>
        /// The SHPT type of the object to be created, such
        /// as SHPT.POINT, or SHPT.POLYGON.
        /// </param>
        /// <param name='nShapeId'>
        /// The shapeid to be recorded with this shape.
        /// </param>
        /// <param name='nParts'>
        /// The number of parts for this object.  If this is
        /// zero for ARC, or POLYGON type objects, a single
        /// zero valued part will be created internally.
        /// </param>
        /// <param name='panPartStart'>
        /// The list of zero based start vertices for the rings
        /// (parts) in this object.  The first should always be
        /// zero.  This may be null if nParts is 0.
        /// </param>
        /// <param name='panPartType'>
        /// The type of each of the parts.  This is only meaningful
        /// for MULTIPATCH files.  For all other cases this may
        /// be null, and will be assumed to be SHPP.RING.
        /// </param>
        /// <param name='nVertices'>
        /// The number of vertices being passed in padfX,
        /// padfY, and padfZ.
        /// </param>
        /// <param name='padfX'>
        /// An array of nVertices X coordinates of the vertices
        /// for this object.
        /// </param>
        /// <param name='padfY'>
        /// An array of nVertices Y coordinates of the vertices
        /// for this object.
        /// </param>
        /// <param name='padfZ'>
        /// An array of nVertices Z coordinates of the vertices
        /// for this object.  This may be null in which case
        ///  they are all assumed to be zero.
        /// </param>
        /// <param name='padfM'>
        /// An array of nVertices M (measure values) of the
        /// vertices for this object.  This may be null in which
        /// case they are all assumed to be zero.
        /// </param>
        public static SHPObject
            Create( SHPT nSHPType, int nShapeId, int nParts,
                   int[] panPartStart, int[] panPartType,
                   int nVertices, double[] padfX, double[] padfY,
                   double[] padfZ, double[] padfM )
        
        {
            SHPObject   psObject;
            int         i;
            bool        bHasM, bHasZ;
        
            psObject = new SHPObject();
            psObject.nSHPType = nSHPType;
            psObject.nShapeId = nShapeId;
        
            /* -------------------------------------------------------------------- */
            /*      Establish whether this shape type has M, and Z values.          */
            /* -------------------------------------------------------------------- */
            if( nSHPType == SHPT.ARCM
                || nSHPType == SHPT.POINTM
                || nSHPType == SHPT.POLYGONM
                || nSHPType == SHPT.MULTIPOINTM )
            {
                bHasM = true;
                bHasZ = false;
            }
            else if( nSHPType == SHPT.ARCZ
                     || nSHPType == SHPT.POINTZ
                     || nSHPType == SHPT.POLYGONZ
                     || nSHPType == SHPT.MULTIPOINTZ
                     || nSHPType == SHPT.MULTIPATCH )
            {
                bHasM = true;
                bHasZ = true;
            }
            else
            {
                bHasM = false;
                bHasZ = false;
            }
        
            /* -------------------------------------------------------------------- */
            /*      Capture parts.  Note that part type is optional, and            */
            /*      defaults to ring.                                               */
            /* -------------------------------------------------------------------- */
            if( nSHPType == SHPT.ARC || nSHPType == SHPT.POLYGON
                || nSHPType == SHPT.ARCM || nSHPType == SHPT.POLYGONM
                || nSHPType == SHPT.ARCZ || nSHPType == SHPT.POLYGONZ
                || nSHPType == SHPT.MULTIPATCH )
            {
                psObject.nParts = Math.Max(1,nParts);
        
                psObject.panPartStart = new int[psObject.nParts];
                psObject.panPartType = new int[psObject.nParts];
        
                psObject.panPartStart[0] = 0;
                psObject.panPartType[0] = (int)SHPP.RING;
                
                for( i = 0; i < nParts; i++ )
                {
                    psObject.panPartStart[i] = panPartStart[i];
                    if( panPartType != null )
                        psObject.panPartType[i] = panPartType[i];
                    else
                        psObject.panPartType[i] = (int)SHPP.RING;
                }
            }
        
            /* -------------------------------------------------------------------- */
            /*      Capture vertices.  Note that Z and M are optional, but X and    */
            /*      Y are not.                                                      */
            /* -------------------------------------------------------------------- */
            if( nVertices > 0 )
            {
                psObject.padfX = new double[nVertices];
                psObject.padfY = new double[nVertices];
                psObject.padfZ = new double[nVertices];
                psObject.padfM = new double[nVertices];
        
                c.assert( padfX != null );
                c.assert( padfY != null );
            
                for( i = 0; i < nVertices; i++ )
                {
                    psObject.padfX[i] = padfX[i];
                    psObject.padfY[i] = padfY[i];
                    if( padfZ != null && bHasZ )
                        psObject.padfZ[i] = padfZ[i];
                    if( padfM != null && bHasM )
                        psObject.padfM[i] = padfM[i];
                }
            }
        
            /* -------------------------------------------------------------------- */
            /*      Compute the extents.                                            */
            /* -------------------------------------------------------------------- */
            psObject.nVertices = nVertices;
            psObject.ComputeExtents();
        
            return( psObject );
        }

        /// <summary>
        /// Create a simple (common) shape object.  Destroy with
        /// SHPObject to null.
        /// </summary>
        /// <returns>
        /// SHPObject instance.
        /// </returns>
        /// <param name='nSHPType'>
        /// The SHPT type of the object to be created, such
        /// as SHPT.POINT, or SHPT.POLYGON.
        /// </param>
        /// <param name='nVertices'>
        /// The number of vertices being passed in padfX,
        /// padfY, and padfZ.
        /// </param>
        /// <param name='padfX'>
        /// An array of nVertices X coordinates of the vertices
        /// for this object.
        /// </param>
        /// <param name='padfY'>
        /// An array of nVertices Y coordinates of the vertices
        /// for this object.
        /// </param>
        /// <param name='padfZ'>
        /// An array of nVertices Z coordinates of the vertices
        /// for this object.  This may be null in which case
        /// they are all assumed to be zero.
        /// </param>
        public static SHPObject
            CreateSimple( SHPT nSHPType, int nVertices,
                         double[] padfX, double[] padfY,
                         double[] padfZ )
        
        {
            return( SHPObject.Create( nSHPType, -1, 0, null, null,
                                     nVertices, padfX, padfY, padfZ, null ) );
        }

        /*
        // Destroy method is not necessary in C# because of GC.
        // (only set null)
        /// <summary>
        /// Destroy this instance.
        /// </summary>
        public void Destroy()
        
        {
            SHPObject   psShape = this; // do not free!

            if( psShape.padfX != null )
                c.free( ref psShape.padfX );
            if( psShape.padfY != null )
                c.free( ref psShape.padfY );
            if( psShape.padfZ != null )
                c.free( ref psShape.padfZ );
            if( psShape.padfM != null )
                c.free( ref psShape.padfM );
        
            if( psShape.panPartStart != null )
                c.free( ref psShape.panPartStart );
            if( psShape.panPartType != null )
                c.free( ref psShape.panPartType );
        
            //free( psShape );
        }
        */
    }

    public partial class SHP
    {
        public static string TypeName( SHPT nSHPType )
        
        {
            switch( nSHPType )
            {
              case SHPT.NULL:
                return "NullShape";
        
              case SHPT.POINT:
                return "Point";
        
              case SHPT.ARC:
                return "Arc";
        
              case SHPT.POLYGON:
                return "Polygon";
        
              case SHPT.MULTIPOINT:
                return "MultiPoint";
                
              case SHPT.POINTZ:
                return "PointZ";
        
              case SHPT.ARCZ:
                return "ArcZ";
        
              case SHPT.POLYGONZ:
                return "PolygonZ";
        
              case SHPT.MULTIPOINTZ:
                return "MultiPointZ";
                
              case SHPT.POINTM:
                return "PointM";
        
              case SHPT.ARCM:
                return "ArcM";
        
              case SHPT.POLYGONM:
                return "PolygonM";
        
              case SHPT.MULTIPOINTM:
                return "MultiPointM";
        
              case SHPT.MULTIPATCH:
                return "MultiPatch";
        
              default:
                return "UnknownShapeType";
            }
        }
        
        public static string PartTypeName( SHPP nPartType )
        
        {
            switch( nPartType )
            {
              case SHPP.TRISTRIP:
                return "TriangleStrip";
                
              case SHPP.TRIFAN:
                return "TriangleFan";
        
              case SHPP.OUTERRING:
                return "OuterRing";
        
              case SHPP.INNERRING:
                return "InnerRing";
        
              case SHPP.FIRSTRING:
                return "FirstRing";
        
              case SHPP.RING:
                return "Ring";
        
              default:
                return "UnknownPartType";
            }
        }
    }
}
