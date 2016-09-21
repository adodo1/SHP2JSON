/******************************************************************************
 * shptree.cs
 *
 * Project:  MonoShapelib
 * Purpose:  Implementation of quadtree building and searching functions.
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

#define MAX_SUBNODE_QUAD

using System;
using System.IO;

namespace MonoShapelib
{
    public partial class SHPTree
    {
        #region constants

        /// <summary>
        /// If the following is 0.5, nodes will be split in half.  If it
        /// is 0.6 then each subnode will contain 60% of the parent
        /// node, with 20% representing overlap.  This can be help to
        /// prevent small objects on a boundary from shifting too high
        /// up the tree.
        /// </summary>
        private const double    SHP_SPLIT_RATIO = 0.55;

        #endregion

        /// <summary>
        /// A realloc cover function that will access a NULL pointer as
        /// a valid input.
        /// </summary>
        private static T[] SfRealloc<T>( ref T[] pMem, int nNewSize )
        
        {
            Array.Resize( ref pMem, nNewSize );
            return pMem;
        }

        /// <summary>
        /// Initialize a tree node.
        /// </summary>
        private static SHPTreeNode NodeCreate( double[] padfBoundsMin,
                                               double[] padfBoundsMax )

        {
            SHPTreeNode psTreeNode;

            psTreeNode = new SHPTreeNode();

            psTreeNode.nShapeCount = 0;
            psTreeNode.panShapeIds = null;
            psTreeNode.papsShapeObj = null;

            psTreeNode.nSubNodes = 0;

            if( padfBoundsMin != null )
                c.memcpy( psTreeNode.adfBoundsMin, padfBoundsMin, sizeof(double) * 4 );

            if( padfBoundsMax != null )
                c.memcpy( psTreeNode.adfBoundsMax, padfBoundsMax, sizeof(double) * 4 );

            return psTreeNode;
        }


        /************************************************************************/
        /*                           SHPCreateTree()                            */
        /************************************************************************/
        public static SHPTree Create( SHPHandle hSHP, int nDimension, int nMaxDepth,
                                    double[] padfBoundsMin, double[] padfBoundsMax )

        {
            SHPTree psTree;

            if( padfBoundsMin == null && hSHP == null )
                return null;

            /* -------------------------------------------------------------------- */
            /*      Allocate the tree object                                        */
            /* -------------------------------------------------------------------- */
            psTree = new SHPTree();

            psTree.hSHP = hSHP;
            psTree.nMaxDepth = nMaxDepth;
            psTree.nDimension = nDimension;

            /* -------------------------------------------------------------------- */
            /*      If no max depth was defined, try to select a reasonable one     */
            /*      that implies approximately 8 shapes per node.                   */
            /* -------------------------------------------------------------------- */
            if( psTree.nMaxDepth == 0 && hSHP != null )
            {
                int nMaxNodeCount = 1;
                int nShapeCount;
                SHPT nShapeType;

                hSHP.GetInfo( out nShapeCount, out nShapeType, null, null );
                while( nMaxNodeCount*4 < nShapeCount )
                {
                    psTree.nMaxDepth += 1;
                    nMaxNodeCount = nMaxNodeCount * 2;
                }
            }

            /* -------------------------------------------------------------------- */
            /*      Allocate the root node.                                         */
            /* -------------------------------------------------------------------- */
            psTree.psRoot = NodeCreate( padfBoundsMin, padfBoundsMax );

            /* -------------------------------------------------------------------- */
            /*      Assign the bounds to the root node.  If none are passed in,     */
            /*      use the bounds of the provided file otherwise the create        */
            /*      function will have already set the bounds.                      */
            /* -------------------------------------------------------------------- */
            if( padfBoundsMin == null )
            {
                int nShapeCount;
                SHPT nShapeType;

                hSHP.GetInfo( out nShapeCount, out nShapeType,
                            psTree.psRoot.adfBoundsMin, 
                            psTree.psRoot.adfBoundsMax );
            }

            /* -------------------------------------------------------------------- */
            /*      If we have a file, insert all it's shapes into the tree.        */
            /* -------------------------------------------------------------------- */
            if( hSHP != null )
            {
                int iShape, nShapeCount;
                SHPT    nShapeType;
                
                hSHP.GetInfo( out nShapeCount, out nShapeType, null, null );

                for( iShape = 0; iShape < nShapeCount; iShape++ )
                {
                    SHPObject   psShape;
                    
                    psShape = hSHP.ReadObject( iShape );
                    psTree.AddShapeId( psShape );
                    psShape = null;
                }
            }        

            return psTree;
        }

        /************************************************************************/
        /*                         SHPDestroyTreeNode()                         */
        /************************************************************************/
        private static void DestroyNode( ref SHPTreeNode psTreeNode )

        {
            int     i;
            
            for( i = 0; i < psTreeNode.nSubNodes; i++ )
            {
                if( psTreeNode.apsSubNode[i] != null )
                    DestroyNode( ref psTreeNode.apsSubNode[i] );
            }
            
            if( psTreeNode.panShapeIds != null )
                c.free( ref psTreeNode.panShapeIds );

            if( psTreeNode.papsShapeObj != null )
            {
                for( i = 0; i < psTreeNode.nShapeCount; i++ )
                {
                    if( psTreeNode.papsShapeObj[i] != null )
                        psTreeNode.papsShapeObj[i] = null;
                }

                c.free( ref psTreeNode.papsShapeObj );
            }

            c.free( ref psTreeNode );
        }

        /************************************************************************/
        /*                           SHPDestroyTree()                           */
        /************************************************************************/
        public void Destroy()

        {
            SHPTree psTree = this;  /* do not free! */
            DestroyNode( ref psTree.psRoot );
            //c.free( ref psTree );
        }

        /************************************************************************/
        /*                       SHPCheckBoundsOverlap()                        */
        /*                                                                      */
        /*      Do the given boxes overlap at all?                              */
        /************************************************************************/
        public static bool CheckBoundsOverlap(
                                double[] padfBox1Min, double[] padfBox1Max,
                                double[] padfBox2Min, double[] padfBox2Max,
                                int nDimension )

        {
            int     iDim;

            for( iDim = 0; iDim < nDimension; iDim++ )
            {
                if( padfBox2Max[iDim] < padfBox1Min[iDim] )
                    return false;
                
                if( padfBox1Max[iDim] < padfBox2Min[iDim] )
                    return false;
            }

            return true;
        }

        /************************************************************************/
        /*                      SHPCheckObjectContained()                       */
        /*                                                                      */
        /*      Does the given shape fit within the indicated extents?          */
        /************************************************************************/
        private static bool CheckObjectContained( SHPObject psObject, int nDimension,
                                   double[] padfBoundsMin, double[] padfBoundsMax )

        {
            if( psObject.dfXMin < padfBoundsMin[0]
                || psObject.dfXMax > padfBoundsMax[0] )
                return false;
            
            if( psObject.dfYMin < padfBoundsMin[1]
                || psObject.dfYMax > padfBoundsMax[1] )
                return false;

            if( nDimension == 2 )
                return true;
            
            if( psObject.dfZMin < padfBoundsMin[2]
                || psObject.dfZMax < padfBoundsMax[2] )
                return false;
                
            if( nDimension == 3 )
                return true;

            if( psObject.dfMMin < padfBoundsMin[3]
                || psObject.dfMMax < padfBoundsMax[3] )
                return false;

            return true;
        }

        /************************************************************************/
        /*                         SHPTreeSplitBounds()                         */
        /*                                                                      */
        /*      Split a region into two subregion evenly, cutting along the     */
        /*      longest dimension.                                              */
        /************************************************************************/
        public static void SplitBounds(
                            double[] padfBoundsMinIn, double[] padfBoundsMaxIn,
                            double[] padfBoundsMin1, double[] padfBoundsMax1,
                            double[] padfBoundsMin2, double[] padfBoundsMax2 )

        {
            /* -------------------------------------------------------------------- */
            /*      The output bounds will be very similar to the input bounds,     */
            /*      so just copy over to start.                                     */
            /* -------------------------------------------------------------------- */
            c.memcpy( padfBoundsMin1, padfBoundsMinIn, sizeof(double) * 4 );
            c.memcpy( padfBoundsMax1, padfBoundsMaxIn, sizeof(double) * 4 );
            c.memcpy( padfBoundsMin2, padfBoundsMinIn, sizeof(double) * 4 );
            c.memcpy( padfBoundsMax2, padfBoundsMaxIn, sizeof(double) * 4 );
            
            /* -------------------------------------------------------------------- */
            /*      Split in X direction.                                           */
            /* -------------------------------------------------------------------- */
            if( (padfBoundsMaxIn[0] - padfBoundsMinIn[0])
                            > (padfBoundsMaxIn[1] - padfBoundsMinIn[1]) )
            {
                double  dfRange = padfBoundsMaxIn[0] - padfBoundsMinIn[0];

                padfBoundsMax1[0] = padfBoundsMinIn[0] + dfRange * SHP_SPLIT_RATIO;
                padfBoundsMin2[0] = padfBoundsMaxIn[0] - dfRange * SHP_SPLIT_RATIO;
            }

            /* -------------------------------------------------------------------- */
            /*      Otherwise split in Y direction.                                 */
            /* -------------------------------------------------------------------- */
            else
            {
                double  dfRange = padfBoundsMaxIn[1] - padfBoundsMinIn[1];

                padfBoundsMax1[1] = padfBoundsMinIn[1] + dfRange * SHP_SPLIT_RATIO;
                padfBoundsMin2[1] = padfBoundsMaxIn[1] - dfRange * SHP_SPLIT_RATIO;
            }
        }

        /************************************************************************/
        /*                       SHPTreeNodeAddShapeId()                        */
        /************************************************************************/
        private static bool NodeAddShapeId(
                                SHPTreeNode psTreeNode, SHPObject psObject,
                                int nMaxDepth, int nDimension )

        {
            int     i;
            
            /* -------------------------------------------------------------------- */
            /*      If there are subnodes, then consider wiether this object        */
            /*      will fit in them.                                               */
            /* -------------------------------------------------------------------- */
            if( nMaxDepth > 1 && psTreeNode.nSubNodes > 0 )
            {
                for( i = 0; i < psTreeNode.nSubNodes; i++ )
                {
                    if( CheckObjectContained(psObject, nDimension,
                                                psTreeNode.apsSubNode[i].adfBoundsMin,
                                                psTreeNode.apsSubNode[i].adfBoundsMax))
                    {
                        return NodeAddShapeId( psTreeNode.apsSubNode[i],
                                                psObject, nMaxDepth-1,
                                                nDimension );
                    }
                }
            }

            /* -------------------------------------------------------------------- */
            /*      Otherwise, consider creating four subnodes if could fit into    */
            /*      them, and adding to the appropriate subnode.                    */
            /* -------------------------------------------------------------------- */
#if MAX_SUBNODE_QUAD
            else if( nMaxDepth > 1 && psTreeNode.nSubNodes == 0 )
            {
                double[] adfBoundsMinH1 = new double[4], adfBoundsMaxH1 = new double[4];
                double[] adfBoundsMinH2 = new double[4], adfBoundsMaxH2 = new double[4];
                double[] adfBoundsMin1 = new double[4], adfBoundsMax1 = new double[4];
                double[] adfBoundsMin2 = new double[4], adfBoundsMax2 = new double[4];
                double[] adfBoundsMin3 = new double[4], adfBoundsMax3 = new double[4];
                double[] adfBoundsMin4 = new double[4], adfBoundsMax4 = new double[4];

                SplitBounds( psTreeNode.adfBoundsMin,
                            psTreeNode.adfBoundsMax,
                            adfBoundsMinH1, adfBoundsMaxH1,
                            adfBoundsMinH2, adfBoundsMaxH2 );

                SplitBounds( adfBoundsMinH1, adfBoundsMaxH1,
                            adfBoundsMin1, adfBoundsMax1,
                            adfBoundsMin2, adfBoundsMax2 );

                SplitBounds( adfBoundsMinH2, adfBoundsMaxH2,
                            adfBoundsMin3, adfBoundsMax3,
                            adfBoundsMin4, adfBoundsMax4 );

                if( CheckObjectContained(psObject, nDimension,
                                        adfBoundsMin1, adfBoundsMax1)
                    || CheckObjectContained(psObject, nDimension,
                                            adfBoundsMin2, adfBoundsMax2)
                    || CheckObjectContained(psObject, nDimension,
                                            adfBoundsMin3, adfBoundsMax3)
                    || CheckObjectContained(psObject, nDimension,
                                            adfBoundsMin4, adfBoundsMax4) )
                {
                    psTreeNode.nSubNodes = 4;
                    psTreeNode.apsSubNode[0] = NodeCreate( adfBoundsMin1,
                                                            adfBoundsMax1 );
                    psTreeNode.apsSubNode[1] = NodeCreate( adfBoundsMin2,
                                                            adfBoundsMax2 );
                    psTreeNode.apsSubNode[2] = NodeCreate( adfBoundsMin3,
                                                            adfBoundsMax3 );
                    psTreeNode.apsSubNode[3] = NodeCreate( adfBoundsMin4,
                                                            adfBoundsMax4 );

                    /* recurse back on this node now that it has subnodes */
                    return( NodeAddShapeId( psTreeNode, psObject,
                                            nMaxDepth, nDimension ) );
                }
            }
#else // MAX_SUBNODE_QUAD

            /* -------------------------------------------------------------------- */
            /*      Otherwise, consider creating two subnodes if could fit into     */
            /*      them, and adding to the appropriate subnode.                    */
            /* -------------------------------------------------------------------- */
            else if( nMaxDepth > 1 && psTreeNode.nSubNodes == 0 )
            {
                double[] adfBoundsMin1 = new double[4], adfBoundsMax1 = new double[4];
                double[] adfBoundsMin2 = new double[4], adfBoundsMax2 = new double[4];

                SplitBounds( psTreeNode.adfBoundsMin, psTreeNode.adfBoundsMax,
                            adfBoundsMin1, adfBoundsMax1,
                            adfBoundsMin2, adfBoundsMax2 );

                if( CheckObjectContained(psObject, nDimension,
                                         adfBoundsMin1, adfBoundsMax1))
                {
                    psTreeNode.nSubNodes = 2;
                    psTreeNode.apsSubNode[0] = NodeCreate( adfBoundsMin1,
                                                            adfBoundsMax1 );
                    psTreeNode.apsSubNode[1] = NodeCreate( adfBoundsMin2,
                                                            adfBoundsMax2 );

                    return( NodeAddShapeId( psTreeNode.apsSubNode[0], psObject,
                                            nMaxDepth - 1, nDimension ) );
                }
                else if( CheckObjectContained(psObject, nDimension,
                                              adfBoundsMin2, adfBoundsMax2) )
                {
                    psTreeNode.nSubNodes = 2;
                    psTreeNode.apsSubNode[0] = NodeCreate( adfBoundsMin1,
                                                            adfBoundsMax1 );
                    psTreeNode.apsSubNode[1] = NodeCreate( adfBoundsMin2,
                                                            adfBoundsMax2 );

                    return( NodeAddShapeId( psTreeNode.apsSubNode[1], psObject,
                                            nMaxDepth - 1, nDimension ) );
                }
            }
#endif // MAX_SUBNODE_QUAD

        /* -------------------------------------------------------------------- */
        /*      If none of that worked, just add it to this nodes list.         */
        /* -------------------------------------------------------------------- */
            psTreeNode.nShapeCount++;

            psTreeNode.panShapeIds =
                SfRealloc( ref psTreeNode.panShapeIds,
                           psTreeNode.nShapeCount );
            psTreeNode.panShapeIds[psTreeNode.nShapeCount-1] = psObject.nShapeId;

            if( psTreeNode.papsShapeObj != null )
            {
                psTreeNode.papsShapeObj =
                    SfRealloc( ref psTreeNode.papsShapeObj,
                               psTreeNode.nShapeCount );
                psTreeNode.papsShapeObj[psTreeNode.nShapeCount-1] = null;
            }

            return true;
        }

        /************************************************************************/
        /*                         SHPTreeAddShapeId()                          */
        /*                                                                      */
        /*      Add a shape to the tree, but don't keep a pointer to the        */
        /*      object data, just keep the shapeid.                             */
        /************************************************************************/
        public bool AddShapeId( SHPObject psObject )

        {
            SHPTree psTree = this;  /* do not free! */
            return( NodeAddShapeId( psTree.psRoot, psObject,
                                           psTree.nMaxDepth, psTree.nDimension ) );
        }

        /************************************************************************/
        /*                      SHPTreeCollectShapesIds()                       */
        /*                                                                      */
        /*      Work function implementing SHPTreeFindLikelyShapes() on a       */
        /*      tree node by tree node basis.                                   */
        /************************************************************************/
        public void CollectShapeIds( SHPTreeNode psTreeNode,
                                double[] padfBoundsMin, double[] padfBoundsMax,
                                ref int pnShapeCount, ref int pnMaxShapes,
                                ref int[] ppanShapeList )

        {
            SHPTree hTree = this;   /* do not free! */

            int     i;
            
            /* -------------------------------------------------------------------- */
            /*      Does this node overlap the area of interest at all?  If not,    */
            /*      return without adding to the list at all.                       */
            /* -------------------------------------------------------------------- */
            if( !CheckBoundsOverlap( psTreeNode.adfBoundsMin,
                                        psTreeNode.adfBoundsMax,
                                        padfBoundsMin,
                                        padfBoundsMax,
                                        hTree.nDimension ) )
                return;

            /* -------------------------------------------------------------------- */
            /*      Grow the list to hold the shapes on this node.                  */
            /* -------------------------------------------------------------------- */
            if( pnShapeCount + psTreeNode.nShapeCount > pnMaxShapes )
            {
                pnMaxShapes = (pnShapeCount + psTreeNode.nShapeCount) * 2 + 20;
                ppanShapeList =
                    SfRealloc(ref ppanShapeList,pnMaxShapes);
            }

            /* -------------------------------------------------------------------- */
            /*      Add the local nodes shapeids to the list.                       */
            /* -------------------------------------------------------------------- */
            for( i = 0; i < psTreeNode.nShapeCount; i++ )
            {
                ppanShapeList[pnShapeCount++] = psTreeNode.panShapeIds[i];
            }
            
            /* -------------------------------------------------------------------- */
            /*      Recurse to subnodes if they exist.                              */
            /* -------------------------------------------------------------------- */
            for( i = 0; i < psTreeNode.nSubNodes; i++ )
            {
                if( psTreeNode.apsSubNode[i] != null )
                    CollectShapeIds( psTreeNode.apsSubNode[i],
                                            padfBoundsMin, padfBoundsMax,
                                            ref pnShapeCount, ref pnMaxShapes,
                                            ref ppanShapeList );
            }
        }

        /************************************************************************/
        /*                      SHPTreeFindLikelyShapes()                       */
        /*                                                                      */
        /*      Find all shapes within tree nodes for which the tree node       */
        /*      bounding box overlaps the search box.  The return value is      */
        /*      an array of shapeids terminated by a -1.  The shapeids will     */
        /*      be in order, as hopefully this will result in faster (more      */
        /*      sequential) reading from the file.                              */
        /************************************************************************/

        public int[] FindLikelyShapes(
                                 double[] padfBoundsMin, double[] padfBoundsMax,
                                 out int pnShapeCount )

        {
            SHPTree hTree = this;   /* do not free! */
            int[]   panShapeList=null;
            int     nMaxShapes = 0;

            /* -------------------------------------------------------------------- */
            /*      Perform the search by recursive descent.                        */
            /* -------------------------------------------------------------------- */
            pnShapeCount = 0;

            CollectShapeIds( hTree.psRoot,
                                    padfBoundsMin, padfBoundsMax,
                                    ref pnShapeCount, ref nMaxShapes,
                                    ref panShapeList );

            /* -------------------------------------------------------------------- */
            /*      Sort the id array                                               */
            /* -------------------------------------------------------------------- */

            Array.Sort(panShapeList);

            return panShapeList;
        }

        /************************************************************************/
        /*                          SHPTreeNodeTrim()                           */
        /*                                                                      */
        /*      This is the recurve version of SHPTreeTrimExtraNodes() that     */
        /*      walks the tree cleaning it up.                                  */
        /************************************************************************/

        private static bool NodeTrim( SHPTreeNode psTreeNode )

        {
            int     i;

            /* -------------------------------------------------------------------- */
            /*      Trim subtrees, and free subnodes that come back empty.          */
            /* -------------------------------------------------------------------- */
            for( i = 0; i < psTreeNode.nSubNodes; i++ )
            {
                if( NodeTrim( psTreeNode.apsSubNode[i] ) )
                {
                    DestroyNode( ref psTreeNode.apsSubNode[i] );

                    psTreeNode.apsSubNode[i] =
                        psTreeNode.apsSubNode[psTreeNode.nSubNodes-1];

                    psTreeNode.nSubNodes--;

                    i--; /* process the new occupant of this subnode entry */
                }
            }

            /* -------------------------------------------------------------------- */
            /*      We should be trimmed if we have no subnodes, and no shapes.     */
            /* -------------------------------------------------------------------- */
            return( psTreeNode.nSubNodes == 0 && psTreeNode.nShapeCount == 0 );
        }

        /************************************************************************/
        /*                       SHPTreeTrimExtraNodes()                        */
        /*                                                                      */
        /*      Trim empty nodes from the tree.  Note that we never trim an     */
        /*      empty root node.                                                */
        /************************************************************************/

        public void TrimExtraNodes()

        {
            SHPTree hTree = this;   /* do not free! */
            NodeTrim( hTree.psRoot );
        }
    }
}
