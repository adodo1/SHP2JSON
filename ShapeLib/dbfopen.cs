/******************************************************************************
 * dbfopen.cs
 *
 * Project:  MonoShapelib
 * Purpose:  Implementation of .dbf access API.
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

#define TRIM_DBF_WHITESPACE

using System;
using System.IO;

namespace MonoShapelib
{
    public partial class DBFHandle : IDisposable
    {
        #region Methods

        public void Dispose()
        {
            Close();
        }

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
        /// This is called to write out the file header, and field
        /// descriptions before writing any actual data records.  This
        /// also computes all the DBFDataSet field offset/size/decimals
        /// and so forth values.
        /// </summary>
        private void WriteHeader()
        
        {
            DBFHandle   psDBF = this;   /* do not free! */

            byte[]  abyHeader = new byte[XBASE_FLDHDR_SZ];
            int     i;
        
            if( !psDBF.bNoHeader )
                return;
        
            psDBF.bNoHeader = false;
        
            /* -------------------------------------------------------------------- */
            /*      Initialize the file header information.                         */
            /* -------------------------------------------------------------------- */
            for( i = 0; i < XBASE_FLDHDR_SZ; i++ )
                abyHeader[i] = 0;
        
            abyHeader[0] = 0x03;        /* memo field? - just copying   */
        
            /* date updated on close, record count preset at zero */
        
            abyHeader[8] = (byte)(psDBF.nHeaderLength % 256);
            abyHeader[9] = (byte)(psDBF.nHeaderLength / 256);
            
            abyHeader[10] = (byte)(psDBF.nRecordLength % 256);
            abyHeader[11] = (byte)(psDBF.nRecordLength / 256);
        
            /* -------------------------------------------------------------------- */
            /*      Write the initial 32 byte file header, and all the field        */
            /*      descriptions.                                                   */
            /* -------------------------------------------------------------------- */
            c.fseek( psDBF.fp, 0, 0 );
            c.fwrite( abyHeader, XBASE_FLDHDR_SZ, 1, psDBF.fp );
            c.fwrite( psDBF.pszHeader, XBASE_FLDHDR_SZ, psDBF.nFields, psDBF.fp );
        
            /* -------------------------------------------------------------------- */
            /*      Write out the newline character if there is room for it.        */
            /* -------------------------------------------------------------------- */
            if( psDBF.nHeaderLength > 32*psDBF.nFields + 32 )
            {
                byte[]  cNewline = new byte[1];
        
                cNewline[0] = 0x0d;
                c.fwrite( cNewline, 1, 1, psDBF.fp );
            }
        }
        
        /// <summary>
        /// Write out the current record if there is one.
        /// </summary>
        public void FlushRecord()
        
        {
            DBFHandle   psDBF = this;   /* do not free! */

            int     nRecordOffset;
        
            if( psDBF.bCurrentRecordModified && psDBF.nCurrentRecord > -1 )
            {
                psDBF.bCurrentRecordModified = false;
                
                nRecordOffset = psDBF.nRecordLength * psDBF.nCurrentRecord 
                                                             + psDBF.nHeaderLength;
                
                c.fseek( psDBF.fp, nRecordOffset, 0 );
                c.fwrite( psDBF.pszCurrentRecord, psDBF.nRecordLength, 1, psDBF.fp );
            }
        }
        
        /// <summary>
        /// Open a .dbf file.
        /// </summary>
        /// <param name='pszFilename'>
        /// The name of the xBase (.dbf) file to access.
        /// </param>
        /// <param name='pszAccess'>
        /// The fopen() style access string.  At this time only
        /// "rb" (read-only binary) and "rb+" (read/write binary)
        /// should be used.
        /// </param>
        public static DBFHandle Open( string pszFilename, string pszAccess )
        
        {
            DBFHandle   psDBF;
            byte[]      pabyBuf;
            int         nFields, nHeadLen, nRecLen, iField, i;
            string      pszBasename, pszFullname;
        
            /* -------------------------------------------------------------------- */
            /*      We only allow the access strings "rb" and "r+".                 */
            /* -------------------------------------------------------------------- */
            if( c.strcmp(pszAccess,"r") != 0 && c.strcmp(pszAccess,"r+") != 0 
                && c.strcmp(pszAccess,"rb") != 0 && c.strcmp(pszAccess,"rb+") != 0
                && c.strcmp(pszAccess,"r+b") != 0 )
                return( null );
        
            if( c.strcmp(pszAccess,"r") == 0 )
                pszAccess = "rb";
         
            if( c.strcmp(pszAccess,"r+") == 0 )
                pszAccess = "rb+";
        
            /* -------------------------------------------------------------------- */
            /*      Compute the base (layer) name.  If there is any extension       */
            /*      on the passed in filename we will strip it off.                 */
            /* -------------------------------------------------------------------- */
            pszBasename = null;
            c.strcpy( ref pszBasename, pszFilename );
            for( i = c.strlenp(pszBasename)-1; 
             i > 0 && pszBasename[i] != '.' && pszBasename[i] != '/'
                   && pszBasename[i] != '\\';
             i-- ) {}

            if( pszBasename[i] == '.' )
                pszBasename = pszBasename.Substring(0, i);

            pszFullname = null;
            c.sprintf( ref pszFullname, "{0}.dbf", pszBasename );
                
            psDBF = new DBFHandle();
            psDBF.fp = c.fopen( pszFullname, pszAccess );
        
            if( psDBF.fp == null )
            {
                c.sprintf( ref pszFullname, "{0}.DBF", pszBasename );
                psDBF.fp = c.fopen(pszFullname, pszAccess );
            }
            
            c.free( ref pszBasename );
            c.free( ref pszFullname );
            
            if( psDBF.fp == null )
            {
                c.free( ref psDBF );
                return( null );
            }
        
            psDBF.bNoHeader = false;
            psDBF.nCurrentRecord = -1;
            psDBF.bCurrentRecordModified = false;
        
            /* -------------------------------------------------------------------- */
            /*  Read Table Header info                                              */
            /* -------------------------------------------------------------------- */
            pabyBuf = new byte[500];
            if( c.fread( pabyBuf, 32, 1, psDBF.fp ) != 1 )
            {
                c.fclose( psDBF.fp );
                c.free( ref pabyBuf );
                c.free( ref psDBF );
                return null;
            }
        
            psDBF.nRecords = 
             pabyBuf[4] + pabyBuf[5]*256 + pabyBuf[6]*256*256 + pabyBuf[7]*256*256*256;
        
            psDBF.nHeaderLength = nHeadLen = pabyBuf[8] + pabyBuf[9]*256;
            psDBF.nRecordLength = nRecLen = pabyBuf[10] + pabyBuf[11]*256;
            
            psDBF.nFields = nFields = (nHeadLen - 32) / 32;
        
            psDBF.pszCurrentRecord = new byte[nRecLen];
        
            /* -------------------------------------------------------------------- */
            /*  Read in Field Definitions                                           */
            /* -------------------------------------------------------------------- */
            
            pabyBuf = SfRealloc(ref pabyBuf,nHeadLen);
            psDBF.pszHeader = pabyBuf;
        
            c.fseek( psDBF.fp, 32, 0 );
            if( c.fread( pabyBuf, nHeadLen-32, 1, psDBF.fp ) != 1 )
            {
                c.fclose( psDBF.fp );
                c.free( ref pabyBuf );
                c.free( ref psDBF );
                return null;
            }
        
            psDBF.panFieldOffset = new int[nFields];
            psDBF.panFieldSize = new int[nFields];
            psDBF.panFieldDecimals = new int[nFields];
            psDBF.pachFieldType = new char[nFields];
        
            for( iField = 0; iField < nFields; iField++ )
            {
                byte[]  pabyFInfo = new byte[32];
                // emurate pointer by copy
                Buffer.BlockCopy( pabyBuf, iField*32, pabyFInfo, 0, 32 );
                
                if( pabyFInfo[11] == 'N' || pabyFInfo[11] == 'F' )
                {
                    psDBF.panFieldSize[iField] = pabyFInfo[16];
                    psDBF.panFieldDecimals[iField] = pabyFInfo[17];
                }
                else
                {
                    psDBF.panFieldSize[iField] = pabyFInfo[16] + pabyFInfo[17]*256;
                    psDBF.panFieldDecimals[iField] = 0;
                }
                
                psDBF.pachFieldType[iField] = (char) pabyFInfo[11];
                if( iField == 0 )
                    psDBF.panFieldOffset[iField] = 1;
                else
                    psDBF.panFieldOffset[iField] = 
                      psDBF.panFieldOffset[iField-1] + psDBF.panFieldSize[iField-1];
            }
        
            return( psDBF );
        }

        /// <summary>
        /// Close the .dbf file.
        /// </summary>
        public void Close()
        {
            DBFHandle   psDBF = this;   /* do not free! */

            /* -------------------------------------------------------------------- */
            /*      Write out header if not already written.                        */
            /* -------------------------------------------------------------------- */
            if( psDBF.bNoHeader )
                WriteHeader();
        
            FlushRecord();
        
            /* -------------------------------------------------------------------- */
            /*      Update last access date, and number of records if we have       */
            /*      write access.                                                   */
            /* -------------------------------------------------------------------- */
            if( psDBF.bUpdated )
            {
                byte[]    abyFileHeader = new byte[32];
                
                c.fseek( psDBF.fp, 0, 0 );
                c.fread( abyFileHeader, 32, 1, psDBF.fp );
                
                abyFileHeader[1] = 95;          /* YY */
                abyFileHeader[2] = 7;           /* MM */
                abyFileHeader[3] = 26;          /* DD */
                
                abyFileHeader[4] = (byte)(psDBF.nRecords % 256);
                abyFileHeader[5] = (byte)((psDBF.nRecords/256) % 256);
                abyFileHeader[6] = (byte)((psDBF.nRecords/(256*256)) % 256);
                abyFileHeader[7] = (byte)((psDBF.nRecords/(256*256*256)) % 256);
                
                c.fseek( psDBF.fp, 0, 0 );
                c.fwrite( abyFileHeader, 32, 1, psDBF.fp );
            }
        
            /* -------------------------------------------------------------------- */
            /*      Close, and free resources.                                      */
            /* -------------------------------------------------------------------- */
            c.fclose( psDBF.fp );
        
            if( psDBF.panFieldOffset != null )
            {
                c.free( ref psDBF.panFieldOffset );
                c.free( ref psDBF.panFieldSize );
                c.free( ref psDBF.panFieldDecimals );
                c.free( ref psDBF.pachFieldType );
            }
        
            c.free( ref psDBF.pszHeader );
            c.free( ref psDBF.pszCurrentRecord );
        
            //c.free( ref psDBF );
        
            /*
            if( pszStringField != null )
            {
                c.free( ref pszStringField );
                pszStringField = null;
                nStringFieldLen = 0;
            }
            */
        }
        
        /// <summary>
        /// Create a new .dbf file.
        /// </summary>
        /// <param name='pszFilename'>
        /// The name of the xBase (.dbf) file to create.
        /// </param>
        public static DBFHandle Create( string pszFilename )
        
        {
            DBFHandle    psDBF;
            FileStream   fp;
            string  pszFullname, pszBasename;
            int     i;
        
            /* -------------------------------------------------------------------- */
            /*      Compute the base (layer) name.  If there is any extension       */
            /*      on the passed in filename we will strip it off.                 */
            /* -------------------------------------------------------------------- */
            pszBasename = null;
            c.strcpy( ref pszBasename, pszFilename );
            for( i = c.strlenp(pszBasename)-1; 
             i > 0 && pszBasename[i] != '.' && pszBasename[i] != '/'
                   && pszBasename[i] != '\\';
             i-- ) {}

            if( pszBasename[i] == '.' )
                pszBasename = pszBasename.Substring(0, i);

            pszFullname = null;
            c.sprintf( ref pszFullname, "{0}.dbf", pszBasename );
            c.free( ref pszBasename );
        
            /* -------------------------------------------------------------------- */
            /*      Create the file.                                                */
            /* -------------------------------------------------------------------- */
            fp = c.fopen( pszFullname, "wb" );
            if( fp == null )
                return( null );
        
            c.fputc( 0, fp ); // TODO:fputc
            c.fclose( fp );
        
            fp = c.fopen( pszFullname, "rb+" );
            if( fp == null )
                return( null );
        
            c.free( ref pszFullname );
        
            /* -------------------------------------------------------------------- */
            /*      Create the info structure.                                      */
            /* -------------------------------------------------------------------- */
            psDBF = new DBFHandle();
        
            psDBF.fp = fp;
            psDBF.nRecords = 0;
            psDBF.nFields = 0;
            psDBF.nRecordLength = 1;
            psDBF.nHeaderLength = 33;
            
            psDBF.panFieldOffset = null;
            psDBF.panFieldSize = null;
            psDBF.panFieldDecimals = null;
            psDBF.pachFieldType = null;
            psDBF.pszHeader = null;
        
            psDBF.nCurrentRecord = -1;
            psDBF.bCurrentRecordModified = false;
            psDBF.pszCurrentRecord = null;
        
            psDBF.bNoHeader = true;
        
            return( psDBF );
        }
        
        /// <summary>
        /// Add a field to a newly created .dbf file before any records
        /// are written.
        /// </summary>
        /// <returns>
        /// The field number of the new field, or -1 if the addition of
        /// the field failed.
        /// </returns>
        /// <param name='pszFieldName'>
        /// The name of the new field.  At most 11 character will be used.
        /// In order to use the xBase file in some packages it may be
        /// necessary to avoid some special characters in the field names
        /// such as spaces, or arithmetic operators.
        /// </param>
        /// <param name='eType'>
        /// One of FT.String, FT.Integer, FT.Double or FT.Logical
        /// in order to establish the type of the new field.
        /// Note that some valid xBase field types cannot be created
        /// such as date fields.
        /// </param>
        /// <param name='nWidth'>
        /// The width of the field to be created.  For FT.String fields this
        /// establishes the maximum length of string that can be stored.
        /// For FT.Integer this establishes the number of digits of the
        /// largest number that can be represented.
        /// For FT.Double fields this in combination with the nDecimals value
        /// establish the size, and precision of the created field.
        /// </param>
        /// <param name='nDecimals'>
        /// The number of decimal places to reserve for FTDouble fields.
        /// For all other field types this should be zero.  For instance
        /// with nWidth=7, and nDecimals=3 numbers would be formatted
        /// similarly to `123.456'.
        /// </param>
        public int AddField(string pszFieldName, 
                    FT eType, int nWidth, int nDecimals )
        
        {
            DBFHandle   psDBF = this;   /* do not free! */

            byte[]  pszFInfo;
            int     i;
        
            /* -------------------------------------------------------------------- */
            /*      Do some checking to ensure we can add records to this file.     */
            /* -------------------------------------------------------------------- */
            if( psDBF.nRecords > 0 )
                return( -1 );
        
            if( !psDBF.bNoHeader )
                return( -1 );
        
            if( eType != FT.Double && nDecimals != 0 )
                return( -1 );

            if( nWidth < 1 )
                return -1;
        
            /* -------------------------------------------------------------------- */
            /*      SfRealloc all the arrays larger to hold the additional field    */
            /*      information.                                                    */
            /* -------------------------------------------------------------------- */
            psDBF.nFields++;
        
            psDBF.panFieldOffset = 
              SfRealloc( ref psDBF.panFieldOffset, psDBF.nFields );
        
            psDBF.panFieldSize =
              SfRealloc( ref psDBF.panFieldSize, psDBF.nFields );
        
            psDBF.panFieldDecimals =
              SfRealloc( ref psDBF.panFieldDecimals, psDBF.nFields );
        
            psDBF.pachFieldType =
              SfRealloc( ref psDBF.pachFieldType, psDBF.nFields );
        
            /* -------------------------------------------------------------------- */
            /*      Assign the new field information fields.                        */
            /* -------------------------------------------------------------------- */
            psDBF.panFieldOffset[psDBF.nFields-1] = psDBF.nRecordLength;
            psDBF.nRecordLength += nWidth;
            psDBF.panFieldSize[psDBF.nFields-1] = nWidth;
            psDBF.panFieldDecimals[psDBF.nFields-1] = nDecimals;
        
            if( eType == FT.Logical )
                psDBF.pachFieldType[psDBF.nFields-1] = 'L';
            else if( eType == FT.String )
                psDBF.pachFieldType[psDBF.nFields-1] = 'C';
            else
                psDBF.pachFieldType[psDBF.nFields-1] = 'N';
        
            /* -------------------------------------------------------------------- */
            /*      Extend the required header information.                         */
            /* -------------------------------------------------------------------- */
            psDBF.nHeaderLength += 32;
            psDBF.bUpdated = false;
        
            psDBF.pszHeader = SfRealloc(ref psDBF.pszHeader,psDBF.nFields*32);
        
            // emurate pointer by copy
            pszFInfo = new byte[32];
            Buffer.BlockCopy(psDBF.pszHeader, 32 * (psDBF.nFields-1), pszFInfo, 0, 32);
        
            for( i = 0; i < 32; i++ )
                pszFInfo[i] = (byte)'\0';
        
            // TODO:don't use multibyte length
            if( (int) c.strlen(pszFieldName) < 10 )
                c.strncpy( pszFInfo, pszFieldName, c.strlen(pszFieldName));
            else
                c.strncpy( pszFInfo, pszFieldName, 10);
        
            pszFInfo[11] = (byte)psDBF.pachFieldType[psDBF.nFields-1];
        
            if( eType == FT.String )
            {
                pszFInfo[16] = (byte)(nWidth % 256);
                pszFInfo[17] = (byte)(nWidth / 256);
            }
            else
            {
                pszFInfo[16] = (byte)nWidth;
                pszFInfo[17] = (byte)nDecimals;
            }

            // back to pointer source
            Buffer.BlockCopy(pszFInfo, 0, psDBF.pszHeader, 32 * (psDBF.nFields-1), 32);
            
            /* -------------------------------------------------------------------- */
            /*      Make the current record buffer appropriately larger.            */
            /* -------------------------------------------------------------------- */
            psDBF.pszCurrentRecord = SfRealloc(ref psDBF.pszCurrentRecord,
                                                psDBF.nRecordLength);
        
            return( psDBF.nFields-1 );
        }
        
        /// <summary>
        /// Read one of the attribute fields of a record.
        /// </summary>
        private object ReadAttribute(int hEntity, int iField,
                                      char chReqType )
        
        {
            DBFHandle   psDBF = this;   /* do not free! */

            int     nRecordOffset;
            byte[]  pabyRec;
            object  pReturnField = null;
        
            double  dDoubleField;
            int     nStringFieldLen = 0;
            byte[]  pszStringField = null;
            string  sStringField = null;
        
            /* -------------------------------------------------------------------- */
            /*      Verify selection.                                               */
            /* -------------------------------------------------------------------- */
            if( hEntity < 0 || hEntity >= psDBF.nRecords )
                return( null );
        
            if( iField < 0 || iField >= psDBF.nFields )
                return( null );
        
            /* -------------------------------------------------------------------- */
            /*      Have we read the record?                                        */
            /* -------------------------------------------------------------------- */
            if( psDBF.nCurrentRecord != hEntity )
            {
                FlushRecord();
                
                nRecordOffset = psDBF.nRecordLength * hEntity + psDBF.nHeaderLength;
                
                if( c.fseek( psDBF.fp, nRecordOffset, 0 ) != 0 )
                {
                    c.fprintf( Console.Error, "fseek({0}) failed on DBF file.\n",
                             nRecordOffset );
                    return null;
                }
        
                if( c.fread( psDBF.pszCurrentRecord, psDBF.nRecordLength, 
                           1, psDBF.fp ) != 1 )
                {
                    c.fprintf( Console.Error, "fread({0}) failed on DBF file.\n",
                             psDBF.nRecordLength );
                    return null;
                }
        
                psDBF.nCurrentRecord = hEntity;
            }
        
            pabyRec = psDBF.pszCurrentRecord;
        
            /* -------------------------------------------------------------------- */
            /*      Ensure our field buffer is large enough to hold this buffer.    */
            /* -------------------------------------------------------------------- */
            if( psDBF.panFieldSize[iField]+1 > nStringFieldLen )
            {
                nStringFieldLen = psDBF.panFieldSize[iField]*2 + 10;
                pszStringField = SfRealloc(ref pszStringField,nStringFieldLen);
            }
        
            /* -------------------------------------------------------------------- */
            /*      Extract the requested field.                                    */
            /* -------------------------------------------------------------------- */
            c.strncpy( pszStringField, 
                 pabyRec, psDBF.panFieldOffset[iField],
                 psDBF.panFieldSize[iField] );
            pszStringField[psDBF.panFieldSize[iField]] = (byte)'\0';
        
            // decode to string
            sStringField = c.cpg.GetString( pszStringField ).TrimEnd( '\0' );
            pReturnField = sStringField;
        
            /* -------------------------------------------------------------------- */
            /*      Decode the field.                                               */
            /* -------------------------------------------------------------------- */
            if( chReqType == 'N' )
            {
                dDoubleField = c.atof(sStringField);
        
                pReturnField = dDoubleField;
            }
        
            /* -------------------------------------------------------------------- */
            /*      Should we trim white space off the string attribute value?      */
            /* -------------------------------------------------------------------- */
#if TRIM_DBF_WHITESPACE
            else
            {
                pReturnField = sStringField.Trim();
            }
#endif
            
            return( pReturnField );
        }
        
        /// <summary>
        /// Read an integer attribute.
        /// </summary>
        /// <returns>
        /// The value of one field as an integer.
        /// </returns>
        /// <param name='iRecord'>
        /// The record number (shape number) from which the field value
        /// should be read.
        /// </param>
        /// <param name='iField'>
        /// The field within the selected record that should be read.
        /// </param>
        public int ReadIntegerAttribute( int iRecord, int iField )
        
        {
            object  pdValue;
        
            pdValue = ReadAttribute( iRecord, iField, 'N' );
        
            if( pdValue == null )
                return 0;
            else
                return( Convert.ToInt32( pdValue ) );
        }
        
        /// <summary>
        /// Read a double attribute.
        /// </summary>
        /// <returns>
        /// The value of one field as a double.
        /// </returns>
        /// <param name='iRecord'>
        /// The record number (shape number) from which the field value
        /// should be read.
        /// </param>
        /// <param name='iField'>
        /// The field within the selected record that should be read.
        /// </param>
        public double ReadDoubleAttribute( int iRecord, int iField )
        
        {
            object  pdValue;
        
            pdValue = ReadAttribute( iRecord, iField, 'N' );
        
            if( pdValue == null )
                return 0.0;
            else
                return( (double) pdValue );
        }
        
        /// <summary>
        /// Read a string attribute.
        /// </summary>
        /// <returns>
        /// The value of one field as a string.
        /// </returns>
        /// <param name='iRecord'>
        /// The record number (shape number) from which the field value
        /// should be read.
        /// </param>
        /// <param name='iField'>
        /// The field within the selected record that should be read.
        /// </param>
        public string ReadStringAttribute( int iRecord, int iField )
        
        {
            return( (string) ReadAttribute( iRecord, iField, 'C' ) );
        }

        /// <summary>
        /// Read a logical attribute.
        /// </summary>
        /// <returns>
        /// The value of one field as a logical character.
        /// </returns>
        /// <param name='iRecord'>
        /// The record number (shape number) from which the field value
        /// should be read.
        /// </param>
        /// <param name='iField'>
        /// The field within the selected record that should be read.
        /// </param>
        public string ReadLogicalAttribute( int iRecord, int iField )

        {
            return( (string) ReadAttribute( iRecord, iField, 'L' ) );
        }

        /// <summary>
        /// Return true if value for field is null.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the indicated field is null valued otherwise <c>false</c>.
        /// </returns>
        /// <param name='iRecord'>
        /// The record number (shape number) from which the field value
        /// should be read.
        /// </param>
        /// <param name='iField'>
        /// The field within the selected record that should be read.
        /// </param>
        /// <remarks>Contributed by Jim Matthews.</remarks>
        public bool IsAttributeNull( int iRecord, int iField )
        
        {
            DBFHandle   psDBF = this;   /* do not free! */

            string  pszValue;
        
            pszValue = ReadStringAttribute( iRecord, iField );
        
            switch(psDBF.pachFieldType[iField])
            {
              case 'N':
              case 'F':
                /* NULL numeric fields have value "****************" */
                return pszValue[0] == '*';
        
              case 'D':
                /* NULL date fields have value "00000000" */
                return c.strncmp(pszValue,"00000000",8) == 0;
        
              case 'L':
                /* NULL boolean fields have value "?" */ 
                return pszValue[0] == '?';
        
              default:
                /* empty string fields are considered NULL */
                return c.strlen(pszValue) == 0;
            }
        }
        
        /// <summary>
        /// Return the number of fields in this table.
        /// </summary>
        /// <returns>
        /// The number of fields currently defined for the indicated
        /// xBase file.
        /// </returns>
        public int GetFieldCount()
        
        {
            DBFHandle   psDBF = this;
            return( psDBF.nFields );
        }
        
        /// <summary>
        /// Return the number of records in this table.
        /// </summary>
        /// <returns>
        /// The number of records that exist on the xBase file currently.
        /// </returns>
        public int GetRecordCount()
        
        {
            DBFHandle   psDBF = this;
            return( psDBF.nRecords );
        }
        
        /// <summary>
        /// Return any requested information about the field.
        /// </summary>
        /// <returns>
        /// The type of the requested field.
        /// </returns>
        /// <param name='iField'>
        /// The field to be queried.  This should be a number between
        /// 0 and n-1, where n is the number fields on the file, a
        /// returned by GetFieldCount().
        /// </param>
        /// <param name='pszFieldName'>
        /// The name of the requested field will be written to
        /// this location.
        /// </param>
        /// <param name='pnWidth'>
        /// The width of the requested field will be returned.
        /// This is the width in characters.
        /// </param>
        /// <param name='pnDecimals'>
        /// the number of decimal places precision defined for the field
        /// will be returned. This is zero for integer fields, or
        /// non-numeric fields.
        /// </param>
        public FT GetFieldInfo( int iField, out string psFieldName,
                         out int pnWidth, out int pnDecimals )
        
        {
            DBFHandle   psDBF = this;   /* do not free! */
            byte[]  pszFieldName;
            psFieldName = null;
            pnWidth = 0;
            pnDecimals = 0;

            if( iField < 0 || iField >= psDBF.nFields )
                return( FT.Invalid );
        
            pnWidth = psDBF.panFieldSize[iField];
        
            pnDecimals = psDBF.panFieldDecimals[iField];
        
            int i;

            pszFieldName = new byte[12];
            c.strncpy( pszFieldName, psDBF.pszHeader, iField*32, 11 );
            pszFieldName[11] = (byte)'\0';
            for( i = 10; i > 0 && pszFieldName[i] == (byte)' '; i-- )
                pszFieldName[i] = (byte)'\0';
            // decode to string
            psFieldName = c.cpg.GetString( pszFieldName ).TrimEnd( '\0' );
        
            if ( psDBF.pachFieldType[iField] == 'L' )
                return( FT.Logical);

            else if( psDBF.pachFieldType[iField] == 'N' 
                     || psDBF.pachFieldType[iField] == 'F'
                     || psDBF.pachFieldType[iField] == 'D' )
            {
                if( psDBF.panFieldDecimals[iField] > 0 )
                    return( FT.Double );
                else
                    return( FT.Integer );
            }
            else
            {
                return( FT.String );
            }
        }
        
        /// <summary>
        /// Write an attribute record to the file.
        /// </summary>
        private bool WriteAttribute( int hEntity, int iField,
                         object pValue )

        {
            DBFHandle   psDBF = this;   /* do not free! */

            int     nRecordOffset, i, j;
            bool    nRetResult = true;
            byte[]  pabyRec;
            string  szSField = new string(' ', 400), szFormat;

            /* -------------------------------------------------------------------- */
            /*      Is this a valid record?                                         */
            /* -------------------------------------------------------------------- */
            if( hEntity < 0 || hEntity > psDBF.nRecords )
                return( false );

            if( psDBF.bNoHeader )
                WriteHeader();

            /* -------------------------------------------------------------------- */
            /*      Is this a brand new record?                                     */
            /* -------------------------------------------------------------------- */
            if( hEntity == psDBF.nRecords )
            {
                FlushRecord();

                psDBF.nRecords++;
                for( i = 0; i < psDBF.nRecordLength; i++ )
                    psDBF.pszCurrentRecord[i] = (byte)' ';

                psDBF.nCurrentRecord = hEntity;
            }

            /* -------------------------------------------------------------------- */
            /*      Is this an existing record, but different than the last one     */
            /*      we accessed?                                                    */
            /* -------------------------------------------------------------------- */
            if( psDBF.nCurrentRecord != hEntity )
            {
                FlushRecord();

                nRecordOffset = psDBF.nRecordLength * hEntity + psDBF.nHeaderLength;

                c.fseek( psDBF.fp, nRecordOffset, 0 );
                c.fread( psDBF.pszCurrentRecord, psDBF.nRecordLength, 1, psDBF.fp );

                psDBF.nCurrentRecord = hEntity;
            }

            pabyRec = psDBF.pszCurrentRecord;

            psDBF.bCurrentRecordModified = true;
            psDBF.bUpdated = true;

            /* -------------------------------------------------------------------- */
            /*      Translate NULL value to valid DBF file representation.          */
            /*                                                                      */
            /*      Contributed by Jim Matthews.                                    */
            /* -------------------------------------------------------------------- */
            if( pValue == null )
            {
                switch(psDBF.pachFieldType[iField])
                {
                case 'N':
                case 'F':
                    /* NULL numeric fields have value "****************" */
                    c.memset( pabyRec, psDBF.panFieldOffset[iField], '*', 
                            psDBF.panFieldSize[iField] );
                    break;

                case 'D':
                    /* NULL date fields have value "00000000" */
                    c.memset( pabyRec, psDBF.panFieldOffset[iField], '0', 
                            psDBF.panFieldSize[iField] );
                    break;

                case 'L':
                    /* NULL boolean fields have value "?" */ 
                    c.memset( pabyRec, psDBF.panFieldOffset[iField], '?', 
                            psDBF.panFieldSize[iField] );
                    break;

                default:
                    /* empty string fields are considered NULL */
                    c.memset( pabyRec, psDBF.panFieldOffset[iField], '\0', 
                            psDBF.panFieldSize[iField] );
                    break;
                }
                return true;
            }

            /* -------------------------------------------------------------------- */
            /*      Assign all the record fields.                                   */
            /* -------------------------------------------------------------------- */
            switch( psDBF.pachFieldType[iField] )
            {
            case 'D':
            case 'N':
            case 'F':
                if( psDBF.panFieldDecimals[iField] == 0 )
                {
                    int     nWidth = psDBF.panFieldSize[iField];

                    if( szSField.Length-2 < nWidth )
                        nWidth = szSField.Length-2;

                    szFormat = string.Concat( "{0,", nWidth.ToString(), "}" );
                    c.sprintf(ref szSField, szFormat, (int)((double)pValue) );
                    if( (int)c.strlen(szSField) > psDBF.panFieldSize[iField] )
                    {
                        szSField = szSField.Substring(psDBF.panFieldSize[iField]);
                        nRetResult = false;
                    }

                    c.strncpy( pabyRec, psDBF.panFieldOffset[iField],
                        szSField, c.strlen(szSField) );
                }
                else
                {
                    int     nWidth = psDBF.panFieldSize[iField];

                    if( szSField.Length-2 < nWidth )
                        nWidth = szSField.Length-2;

                    szFormat = string.Concat( "{0,", nWidth.ToString(), ":F", 
                                             psDBF.panFieldDecimals[iField].ToString(), "}" );
                    c.sprintf(ref szSField, szFormat, (double)pValue );
                    if( (int) c.strlen(szSField) > psDBF.panFieldSize[iField] )
                    {
                        szSField = szSField.Substring(psDBF.panFieldSize[iField]);
                        nRetResult = false;
                    }
                    c.strncpy( pabyRec, psDBF.panFieldOffset[iField],
                        szSField, c.strlen(szSField) );
                }
                break;

            case 'L':
                if (psDBF.panFieldSize[iField] >= 1  && 
                    ((char)pValue == 'F' || (char)pValue == 'T'))
                    pabyRec[psDBF.panFieldOffset[iField]] = (byte)pValue;
                break;

            default:
                byte[]  pszValue = c.cpg.GetBytes( (string)pValue );
                if( (int) c.strlen(pszValue) > psDBF.panFieldSize[iField] )
                {
                    j = psDBF.panFieldSize[iField];
                    nRetResult = false;
                }
                else
                {
                    c.memset( pabyRec, psDBF.panFieldOffset[iField], ' ',
                            psDBF.panFieldSize[iField] );
                    j = c.strlen(pszValue);
                }

                c.strncpy(pabyRec, psDBF.panFieldOffset[iField],
                    pszValue, j );
                break;
            }

            return( nRetResult );
        }
        
        /// <summary>
        /// Write an attribute record to the file, but without any
        /// reformatting based on type.  The provided buffer is written
        /// as is to the field position in the record.
        /// </summary>
        private bool WriteAttributeDirectly( int hEntity, int iField,
                                      byte[] pValue )
        
        {
            DBFHandle   psDBF = this;   /* do not free! */

            int     nRecordOffset, i, j;
            byte[]  pabyRec;
        
            /* -------------------------------------------------------------------- */
            /*      Is this a valid record?                                         */
            /* -------------------------------------------------------------------- */
            if( hEntity < 0 || hEntity > psDBF.nRecords )
                return( false );
        
            if( psDBF.bNoHeader )
                WriteHeader();
        
            /* -------------------------------------------------------------------- */
            /*      Is this a brand new record?                                     */
            /* -------------------------------------------------------------------- */
            if( hEntity == psDBF.nRecords )
            {
                FlushRecord();

                psDBF.nRecords++;
                for( i = 0; i < psDBF.nRecordLength; i++ )
                    psDBF.pszCurrentRecord[i] = (byte)' ';

                psDBF.nCurrentRecord = hEntity;
            }
        
            /* -------------------------------------------------------------------- */
            /*      Is this an existing record, but different than the last one     */
            /*      we accessed?                                                    */
            /* -------------------------------------------------------------------- */
            if( psDBF.nCurrentRecord != hEntity )
            {
                FlushRecord();

                nRecordOffset = psDBF.nRecordLength * hEntity + psDBF.nHeaderLength;

                c.fseek( psDBF.fp, nRecordOffset, 0 );
                c.fread( psDBF.pszCurrentRecord, psDBF.nRecordLength, 1, psDBF.fp );

                psDBF.nCurrentRecord = hEntity;
            }
        
            pabyRec = psDBF.pszCurrentRecord;
        
            /* -------------------------------------------------------------------- */
            /*      Assign all the record fields.                                   */
            /* -------------------------------------------------------------------- */
            if( (int)c.strlen(pValue) > psDBF.panFieldSize[iField] )
                j = psDBF.panFieldSize[iField];
            else
            {
                c.memset( pabyRec, psDBF.panFieldOffset[iField], ' ',
                        psDBF.panFieldSize[iField] );
                j = c.strlen(pValue);
            }
        
            c.strncpy( pabyRec, psDBF.panFieldOffset[iField],
                        pValue, j );
        
            psDBF.bCurrentRecordModified = true;
            psDBF.bUpdated = true;
        
            return( true );
        }
        
        /// <summary>
        /// Write a double attribute.
        /// </summary>
        /// <returns>
        /// If the write succeeds the value <c>true</c>, otherwise <c>false</c>.
        /// </returns>
        /// <param name='iRecord'>
        /// The record number (shape number) to which the field value
        /// should be written.
        /// </param>
        /// <param name='iField'>
        /// The field within the selected record that should be written.
        /// </param>
        /// <param name='dValue'>
        /// The floating point value that should be written.
        /// </param>
        public bool WriteDoubleAttribute( int iRecord, int iField,
                                 double dValue )
        
        {
            return( WriteAttribute( iRecord, iField, dValue ) );
        }
        
        /// <summary>
        /// Write a integer attribute.
        /// </summary>
        /// <returns>
        /// If the write succeeds the value <c>true</c>, otherwise <c>false</c>.
        /// </returns>
        /// <param name='iRecord'>
        /// The record number (shape number) to which the field value
        /// should be written.
        /// </param>
        /// <param name='iField'>
        /// The field within the selected record that should be written.
        /// </param>
        /// <param name='nValue'>
        /// The integer value that should be written.
        /// </param>
        public bool WriteIntegerAttribute( int iRecord, int iField,
                                  int nValue )
        
        {
            return( WriteAttribute( iRecord, iField, nValue ) );
        }
        
        /// <summary>
        /// Write a string attribute.
        /// </summary>
        /// <returns>
        /// If the write succeeds the value <c>true</c>, otherwise <c>false</c>.
        /// </returns>
        /// <param name='iRecord'>
        /// The record number (shape number) to which the field value
        /// should be written.
        /// </param>
        /// <param name='iField'>
        /// The field within the selected record that should be written.
        /// </param>
        /// <param name='pszValue'>
        /// The string to be written to the field.
        /// </param>
        public bool WriteStringAttribute( int iRecord, int iField,
                                 string pszValue )
        
        {
            return( WriteAttribute( iRecord, iField, pszValue ) );
        }
        
        /// <summary>
        /// Write a null attribute.
        /// </summary>
        /// <returns>
        /// If the write succeeds the value <c>true</c>, otherwise <c>false</c>.
        /// </returns>
        /// <param name='iRecord'>
        /// The record number (shape number) to which the field value
        /// should be written.
        /// </param>
        /// <param name='iField'>
        /// The field within the selected record that should be written.
        /// </param>
        public bool WriteNULLAttribute( int iRecord, int iField )
        
        {
            return( WriteAttribute( iRecord, iField, null ) );
        }

        /// <summary>
        /// Write a logical attribute.
        /// </summary>
        /// <returns>
        /// If the write succeeds the value <c>true</c>, otherwise <c>false</c>.
        /// </returns>
        /// <param name='iRecord'>
        /// The record number (shape number) to which the field value
        /// should be written.
        /// </param>
        /// <param name='iField'>
        /// The field within the selected record that should be written.
        /// </param>
        /// <param name='lValue'>
        /// The logical character to be written to the field.
        /// </param>
        public bool WriteLogicalAttribute( int iRecord, int iField,
                       char lValue)

        {
            return( WriteAttribute( iRecord, iField, lValue ) );
        }

        /// <summary>
        /// Write an attribute record to the file.
        /// </summary>
        public bool WriteTuple( int hEntity, byte[] pRawTuple )
        
        {
            DBFHandle   psDBF = this;   /* do not free! */

            int     nRecordOffset, i;
            byte[]  pabyRec;
        
            /* -------------------------------------------------------------------- */
            /*      Is this a valid record?                                         */
            /* -------------------------------------------------------------------- */
            if( hEntity < 0 || hEntity > psDBF.nRecords )
                return( false );
        
            if( psDBF.bNoHeader )
                WriteHeader();
        
            /* -------------------------------------------------------------------- */
            /*      Is this a brand new record?                                     */
            /* -------------------------------------------------------------------- */
            if( hEntity == psDBF.nRecords )
            {
                FlushRecord();

                psDBF.nRecords++;
                for( i = 0; i < psDBF.nRecordLength; i++ )
                    psDBF.pszCurrentRecord[i] = (byte)' ';

                psDBF.nCurrentRecord = hEntity;
            }
        
            /* -------------------------------------------------------------------- */
            /*      Is this an existing record, but different than the last one     */
            /*      we accessed?                                                    */
            /* -------------------------------------------------------------------- */
            if( psDBF.nCurrentRecord != hEntity )
            {
                FlushRecord();

                nRecordOffset = psDBF.nRecordLength * hEntity + psDBF.nHeaderLength;

                c.fseek( psDBF.fp, nRecordOffset, 0 );
                c.fread( psDBF.pszCurrentRecord, psDBF.nRecordLength, 1, psDBF.fp );

                psDBF.nCurrentRecord = hEntity;
            }
        
            pabyRec = psDBF.pszCurrentRecord;
        
            c.memcpy ( pabyRec, pRawTuple,  psDBF.nRecordLength );
        
            psDBF.bCurrentRecordModified = true;
            psDBF.bUpdated = true;
        
            return( true );
        }
        
        /// <summary>
        /// Read one of the attribute fields of a record.
        /// </summary>
        public byte[] ReadTuple( int hEntity )
        
        {
            DBFHandle   psDBF = this;   /* do not free! */

            int     nRecordOffset;
            byte[]  pabyRec;
            byte[]  pReturnTuple = null;
        
            int     nTupleLen = 0;
        
            /* -------------------------------------------------------------------- */
            /*      Have we read the record?                                        */
            /* -------------------------------------------------------------------- */
            if( hEntity < 0 || hEntity >= psDBF.nRecords )
                return( null );
        
            if( psDBF.nCurrentRecord != hEntity )
            {
                FlushRecord();

                nRecordOffset = psDBF.nRecordLength * hEntity + psDBF.nHeaderLength;

                c.fseek( psDBF.fp, nRecordOffset, 0 );
                c.fread( psDBF.pszCurrentRecord, psDBF.nRecordLength, 1, psDBF.fp );

                psDBF.nCurrentRecord = hEntity;
            }
        
            pabyRec = psDBF.pszCurrentRecord;
        
            if ( nTupleLen < psDBF.nRecordLength) {
                nTupleLen = psDBF.nRecordLength;
                pReturnTuple = SfRealloc(ref pReturnTuple, psDBF.nRecordLength);
            }
            
            c.memcpy ( pReturnTuple, pabyRec, psDBF.nRecordLength );
                
            return( pReturnTuple );
        }
        
        /// <summary>
        /// Clones the empty.
        /// </summary>
        public DBFHandle CloneEmpty( string pszFilename ) 
        {
            DBFHandle   psDBF = this;   /* do not free! */
            DBFHandle   newDBF;
        
            newDBF = DBFHandle.Create ( pszFilename );
            if ( newDBF == null ) return ( null ); 

            newDBF.pszHeader = new byte[32 * psDBF.nFields];
            c.memcpy ( newDBF.pszHeader, psDBF.pszHeader, 32 * psDBF.nFields );

            newDBF.nFields = psDBF.nFields;
            newDBF.nRecordLength = psDBF.nRecordLength;
            newDBF.nHeaderLength = 32 * (psDBF.nFields+1);

            newDBF.panFieldOffset = new int[psDBF.nFields]; 
            c.memcpy ( newDBF.panFieldOffset, psDBF.panFieldOffset, sizeof(int) * psDBF.nFields );
            newDBF.panFieldSize = new int[psDBF.nFields]; 
            c.memcpy ( newDBF.panFieldSize, psDBF.panFieldSize, sizeof(int) * psDBF.nFields );
            newDBF.panFieldDecimals = new int[psDBF.nFields]; 
            c.memcpy ( newDBF.panFieldDecimals, psDBF.panFieldDecimals, sizeof(int) * psDBF.nFields );
            newDBF.pachFieldType = new char[psDBF.nFields];
            c.memcpy ( newDBF.pachFieldType, psDBF.pachFieldType, sizeof(int) * psDBF.nFields );

            newDBF.bNoHeader = true;
            newDBF.bUpdated = true;

            newDBF.WriteHeader ();
            newDBF.Close ();

            newDBF = DBFHandle.Open ( pszFilename, "rb+" );

            return ( newDBF );
        }
        
        /// <summary>
        /// Return the DBase field type for the specified field.
        /// </summary>
        /// <returns>
        /// Value can be one of: 'C' (String), 'D' (Date), 'F' (Float),
        /// 'N' (Numeric, with or without decimal), 'L' (Logical),
        /// 'M' (Memo: 10 digits .DBT block ptr)
        /// </returns>
        /// <param name='iField'>
        /// The field index to query.
        /// </param>
        public char GetNativeFieldType( int iField )
        
        {
            DBFHandle   psDBF = this;   /* do not free! */

            if( iField >=0 && iField < psDBF.nFields )
                return psDBF.pachFieldType[iField];
        
            return  ' ';
        }
        
        /// <summary>
        /// Str_to_upper the specified str.
        /// </summary>
        private static void str_to_upper (ref string str)
        {
            /*
            int len;
            short i = -1;
        
            len = strlen (string);
        
            while (++i < len)
                if (isalpha(string[i]) && islower(string[i]))
                    string[i] = toupper ((int)string[i]);
            */
            str = str.ToUpper();
        }
        
        /// <summary>
        /// Get the index number for a field in a .dbf file.
        /// </summary>
        /// <returns>
        /// The index of the field matching this name, or -1 on failure.
        /// </returns>
        /// <param name='pszFieldName'>
        /// Name of the field to search for.
        /// </param>
        /// <remarks>Contributed by Jim Matthews.</remarks>
        public int GetFieldIndex(string pszFieldName)
        
        {
            string        name, name1, name2;
            int           i;
        
            c.strncpy(out name1, pszFieldName,11);
            str_to_upper(ref name1);
        
            for( i = 0; i < GetFieldCount(); i++ )
            {
                int nWidth, nDecimals;
                GetFieldInfo( i, out name, out nWidth, out nDecimals );
                c.strncpy(out name2,name,11);
                str_to_upper(ref name2);
        
                if(c.strncmp(name1,name2,10) != 0)
                    return(i);
            }
            return(-1);
        }

        #endregion
    }
}
