/******************************************************************************
 * shapefil.cs
 *
 * Project:  MonoShapelib
 * Purpose:  Primary definition file for MonoShapelib.
 * Author:   Ko Nagase, geosanak@gmail.com
 *
 ******************************************************************************
 * Copyright (c) 1999, Frank Warmerdam
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

#region Definitions

/// <summary>
/// Should the DBFHandle.ReadStringAttribute() strip leading and
/// trailing white space?
/// </summary>
#define TRIM_DBF_WHITESPACE

/// <summary>
/// Should we write measure values to the Multipatch object?
/// Reportedly ArcView crashes if we do write it, so for now it
/// is disabled.
/// </summary>
#define DISABLE_MULTIPATCH_MEASURE

/// <summary>
/// this can be two or four for binary or quad tree
/// </summary>
#define MAX_SUBNODE_QUAD

#endregion

using System;
using System.IO;

namespace MonoShapelib
{
    #region SHP Support.
    
    public partial class SHPHandle
    {
        #region Fields
    
        public  FileStream  fpSHP;
        public  FileStream  fpSHX;
    
        public  SHPT        nShapeType;             /* ShapeType.* */
        
        public  int         nFileSize;              /* SHP file */
    
        public  int         nRecords;
        public  int         nMaxRecords;
        public  int[]       panRecOffset = null;
        public  int[]       panRecSize = null;
    
        public  double[]    adBoundsMin = new double[4];
        public  double[]    adBoundsMax = new double[4];
    
        public  bool        bUpdated;
    
        public  byte[]      pabyRec;
        public  int         nBufSize;
    
        #endregion
    
        #region Methods
    
        /*
        public static SHPHandle Open( string pszShapeFile, string pszAccess );
        public static SHPHandle Create( string pszShapeFile, SHPT nShapeType );
        public void GetInfo( out int pnEntities, out SHPT pnShapeType,
                            double[] padfMinBound, double[] padfMaxBound );
        
        public SHPObject ReadObject( int iShape );
        public int WriteObject( int iShape, SHPObject psObject );
        
        public int RewindObject( SHPObject psObject );
    
        public void Close();
        */
    
        #endregion
    }
    
    /// <summary>
    /// Shape types (nSHPType)
    /// </summary>
    public enum SHPT
    {
        NULL = 0,
        POINT = 1,
        ARC = 3,
        POLYGON = 5,
        MULTIPOINT = 8,
        POINTZ = 11,
        ARCZ = 13,
        POLYGONZ = 15,
        MULTIPOINTZ = 18,
        POINTM = 21,
        ARCM = 23,
        POLYGONM = 25,
        MULTIPOINTM = 28,
        MULTIPATCH = 31
    }
    
    /// <summary>
    /// Part types - everything but SHPT.MULTIPATCH just uses
    /// SHPP.RING.
    /// </summary>
    public enum SHPP
    {
        TRISTRIP = 0,
        TRIFAN = 1,
        OUTERRING = 2,
        INNERRING = 3,
        FIRSTRING = 4,
        RING = 5
    }
    
    /// <summary>
    /// SHPObject - represents on shape (without attributes) read
    /// from the .shp file.
    /// </summary>
    public partial class SHPObject
    {
        #region Fields
    
        /// <summary>
        /// Shape Type (SHPT.* - see list above).
        /// </summary>
        public  SHPT        nSHPType;
        /// <summary>
        /// Shape Number (-1 is unknown/unassigned).
        /// </summary>
        public  int         nShapeId;
        /// <summary>
        /// # of Parts (0 implies single part with no info).
        /// </summary>
        public  int         nParts;
        /// <summary>
        /// Start Vertex of part.
        /// </summary>
        public  int[]       panPartStart;
        /// <summary>
        /// Part Type (SHPP.RING if not SHPT.MULTIPATCH).
        /// </summary>
        public  int[]       panPartType;
        /// <summary>
        /// Vertex list.
        /// </summary>
        public  int         nVertices;
        public  double[]    padfX;
        public  double[]    padfY;
        public  double[]    padfZ;  /* (all zero if not provided) */
        public  double[]    padfM;  /* (all zero if not provided) */

        public  double      dfXMin; /* Bounds in X, Y, Z and M dimensions */
        public  double      dfYMin;
        public  double      dfZMin;
        public  double      dfMMin;
    
        public  double      dfXMax;
        public  double      dfYMax;
        public  double      dfZMax;
        public  double      dfMMax;
    
        #endregion
    
        #region Methods
    
        /*
        // Destroy method is not necessary in C# because of GC. (only set null)
        //public void Destroy();
        public void ComputeExtents();
        public static SHPObject Create( int nSHPType, int nShapeId,
                            int nParts, int[] panPartStart, int[] panPartType,
                            int nVertices, double[] padfX, double[] padfY,
                            double[] padfZ, double[] padfM );
        public static SHPObject CreateSimple( int nSHPType, int nVertices,
                            double[] padfX, double[] padfY, double[] padfZ );
        */
    
        #endregion
    }
    
    public partial class SHP
    {
        #region Methods

        /*
        public static string TypeName( SHPT nSHPType );
        public static string PartTypeName( SHPP nPartType );
        */

        #endregion
    }
    
    #region Shape quadtree indexing API.

    public class SHPTreeNode
    {
        #region Constants
        
#if MAX_SUBNODE_QUAD
        private const int   MAX_SUBNODE = 4;
#else
        private const int   MAX_SUBNODE = 2;
#endif

        #endregion

        #region Fields
    
        /* region covered by this node */
        public  double[]    adfBoundsMin = new double[4];
        public  double[]    adfBoundsMax = new double[4];
    
        /* list of shapes stored at this node.  The papsShapeObj pointers
           or the whole list can be NULL */
        public  int         nShapeCount;
        public  int[]       panShapeIds;
        public  SHPObject[] papsShapeObj;
    
        public  int         nSubNodes;
        public  SHPTreeNode[] apsSubNode = new SHPTreeNode[MAX_SUBNODE];
    
        #endregion
    }
    
    public partial class SHPTree
    {
        #region Fields
    
        public  SHPHandle   hSHP;
        
        public  int         nMaxDepth;
        public  int         nDimension;
        
        public  SHPTreeNode psRoot;
    
        #endregion
    
        #region Methods
    
        /*
        public static SHPTree
            Create( SHPHandle hSHP, int nDimension, int nMaxDepth,
                    double[] padfBoundsMin, double[] padfBoundsMax );
        public void Destroy();
        
        public int Write( string pszFilename );
        public static SHPTree Read( string pszFilename );
        
        public int AddObject( SHPObject psObject );
        public int AddShapeId( SHPObject psObject );
        public int RemoveShapeId( int nShapeId );
        
        public void TrimExtraNodes();
        
        public int FindLikelyShapes( double[] padfBoundsMin,
                                     double[] padfBoundsMax,
                                     int[] );
        public static int
              CheckBoundsOverlap( double[], double[], double[], double[], int );
        */
    
        #endregion
    }
    
    #endregion

    #endregion

    #region DBF Support.
    
    public partial class DBFHandle
    {
        #region Constants
    
        private const int   XBASE_FLDHDR_SZ = 32;
    
        #endregion
    
        #region Fields
    
        public  FileStream  fp;
    
        public  int         nRecords;
    
        public  int         nRecordLength;
        public  int         nHeaderLength;
        public  int         nFields;
        public  int[]       panFieldOffset;
        public  int[]       panFieldSize;
        public  int[]       panFieldDecimals;
        public  char[]      pachFieldType;
    
        public  byte[]      pszHeader;
    
        public  int         nCurrentRecord;
        public  bool        bCurrentRecordModified;
        public  byte[]      pszCurrentRecord;
        
        public  bool        bNoHeader;
        public  bool        bUpdated;
    
        #endregion
    
        #region Methods
        
        /*
        public static DBFHandle Open( string pszDBFFile, string pszAccess );
        public static DBFHandle Create( string pszDBFFile );
        
        public int GetFieldCount();
        public int GetRecordCount();
        public int AddField( string pszFieldName, FT eType,
                            int nWidth, int nDecimals );
        
        public FT GetFieldInfo( int iField, string pszFieldName,
                                out int pnWidth, out int pnDecimals );
        
        public int GetFieldIndex( string pszFieldName );
        
        public int ReadIntegerAttribute( int iShape, int iField );
        public double ReadDoubleAttribute( int iShape, int iField );
        public string ReadStringAttribute( int iShape, int iField );
        public string ReadLogicalAttribute( int iShape, int iField );
        public int IsAttributeNULL( int iShape, int iField );
        
        public int WriteIntegerAttribute( int iShape, int iField, 
                                        int nFieldValue );
        public int WriteDoubleAttribute( int iShape, int iField,
                                        double dFieldValue );
        public int WriteStringAttribute( int iShape, int iField,
                                        string pszFieldValue );
        public int WriteNULLAttribute( int iShape, int iField );

        public int WriteLogicalAttribute( int iShape, int iField,
                                        char lFieldValue);
                   
        public int WriteAttributeDirectly( int hEntity, int iField,
                                        byte[] pValue );
        public string ReadTuple( int hEntity );
        public int WriteTuple( int hEntity, byte[] pRawTuple );
        
        public DBFHandle CloneEmpty( string pszFilename );
        
        public void Close();
        public char GetNativeFieldType( int iField );
        */
        
        #endregion
    }
    
    public enum FT
    {
        String,
        Integer,
        Double,
        Logical,
        Invalid
    }
    
    #endregion
}