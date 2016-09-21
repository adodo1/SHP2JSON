using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;

namespace MonoShapelib
{
    public class SHPRecord
    {
        #region Private fields
        // Record Header.
        private int recordNumber = -1;
        private int contentLength;

        // Shape type.
        private SHPT shapeType;

        // Bounding box for shape.
        private double xMin = 0;
        private double yMin = 0;
        private double xMax = 0;
        private double yMax = 0;

        // Part indices and points array.
        private Collection<int> parts = new Collection<int>();
        private Collection<double[]> points = new Collection<double[]>();   // 点坐标 分别是 XYZM

        // Shape attributes from a row in the dBASE file.
        private IDictionary<string, object> attributes;
        #endregion Private fields

        #region Constructor
        /// <summary>
        /// Constructor for the ShapeFileRecord class.
        /// </summary>
        public SHPRecord()
        {
        }
        #endregion Constructor

        #region Properties
        /// <summary>
        /// Indicates the record number (or index) which starts at 1.
        /// 图形ID
        /// </summary>
        public int RecordNumber
        {
            get { return this.recordNumber; }
            set { this.recordNumber = value; }
        }
        /// <summary>
        /// Specifies the length of this shape record in 16-bit words.
        /// 不知道
        /// </summary>
        public int ContentLength
        {
            get { return this.contentLength; }
            set { this.contentLength = value; }
        }
        /// <summary>
        /// Specifies the shape type for this record.
        /// 图形类型 点 线 面
        /// </summary>
        public SHPT ShapeType
        {
            get { return this.shapeType; }
            set { this.shapeType = value; }
        }
        /// <summary>
        /// Indicates the minimum x-position of the bounding
        /// box for the shape (expressed in degrees longitude).
        /// 最小X坐标
        /// </summary>
        public double XMin
        {
            get { return this.xMin; }
            set { this.xMin = value; }
        }
        /// <summary>
        /// Indicates the minimum y-position of the bounding
        /// box for the shape (expressed in degrees latitude).
        /// 最小Y坐标
        /// </summary>
        public double YMin
        {
            get { return this.yMin; }
            set { this.yMin = value; }
        }
        /// <summary>
        /// Indicates the maximum x-position of the bounding
        /// box for the shape (expressed in degrees longitude).
        /// 最大X
        /// </summary>
        public double XMax
        {
            get { return this.xMax; }
            set { this.xMax = value; }
        }
        /// <summary>
        /// Indicates the maximum y-position of the bounding
        /// box for the shape (expressed in degrees latitude).
        /// 最大Y
        /// </summary>
        public double YMax
        {
            get { return this.yMax; }
            set { this.yMax = value; }
        }
        /// <summary>
        /// Indicates the number of parts for this shape.
        /// A part is a connected set of points, analogous to
        /// a PathFigure in WPF.
        /// 总共分几段
        /// </summary>
        public int NumberOfParts
        {
            get { return this.parts.Count; }
        }
        /// <summary>
        /// Specifies the total number of points defining
        /// this shape record.
        /// 所有节点总数
        /// </summary>
        public int NumberOfPoints
        {
            get { return this.points.Count; }
        }
        /// <summary>      
        /// A collection of indices for the points array.
        /// Each index identifies the starting point of the
        /// corresponding part (or PathFigure using WPF
        /// terminology).
        /// 每一个分段的起始节点索引
        /// </summary>
        public Collection<int> Parts
        {
            get { return this.parts; }
        }
        /// <summary>
        /// A collection of all of the points defining the
        /// shape record.
        /// 所有节点
        /// </summary>
        public Collection<double[]> Points
        {
            get { return this.points; }
            set { this.points = value; }
        }
        /// <summary>
        /// Access the (dBASE) attribute values associated
        /// with this shape record.
        /// 属性字典
        /// </summary>
        public IDictionary<string, object> Attributes
        {
            get { return this.attributes; }
            set { this.attributes = value; }
        }
        #endregion Properties

        #region Public methods
        /// <summary>
        /// Output some of the fields of the shapefile record.
        /// </summary>
        /// <returns>A string representation of the record.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat( "ShapeFileRecord: RecordNumber={0}, ContentLength={1}, ShapeType={2}",
                this.recordNumber, this.contentLength, this.shapeType );

            return sb.ToString();
        }
        #endregion Public methods
    }
}
