using MonoShapelib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SHP2JSON
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 打开SHP文件
        /// </summary>
        private void toolStripMenuItemOpenSHP_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "SHP数据(*.SHP)|*.SHP";
            if (dialog.ShowDialog() != DialogResult.OK) return;

            ImportSHP(dialog.FileName);
        }
        /// <summary>
        /// 导入SHP文件
        /// </summary>
        /// <param name="sfile"></param>
        private void ImportSHP(string sfile)
        {
            int indexext = sfile.LastIndexOf(".shp", StringComparison.OrdinalIgnoreCase);
            if (indexext < 0) return;
            string dfile = sfile.Substring(0, indexext) + ".dbf";
            SHPHandle hSHP = SHPHandle.Open(sfile, "rb");
            DBFHandle hDBF = DBFHandle.Open(dfile, "rb");

            // 读取DBF信息
            Dictionary<int, Dictionary<string, object>> fieldInfos = new Dictionary<int, Dictionary<string, object>>();
            int fieldCount = hDBF.GetFieldCount();      // 字段总数
            for (int i = 0; i < fieldCount; i++) {
                string szTitle;
                int nWidth;
                int nDecimals;
                FT eType = hDBF.GetFieldInfo(i, out szTitle, out nWidth, out nDecimals);
                fieldInfos[i] = new Dictionary<string, object>() {
                    {"name", szTitle},
                    {"width", nWidth },
                    {"decimals", nDecimals},
                    {"type", eType}
                };
            }

            // 读取SHP信息
            int nEntities;
            SHPT nShapeType;
            double[] adfMinBound = new double[4];
            double[] adfMaxBound = new double[4];
            hSHP.GetInfo(out nEntities, out nShapeType, adfMinBound, adfMaxBound);

            Dictionary<string, object> featureCollection = new Dictionary<string, object>();
            List<object> features = new List<object>();
            featureCollection["type"] = "FeatureCollection";
            featureCollection["features"] = features;

            StreamWriter writer = new StreamWriter("山体保护线.txt", false, Encoding.UTF8);

            writer.Write("{'type': 'FeatureCollection','features': [".Replace('\'', '"'));
            
            for (int irecord = 0; irecord < nEntities; irecord++) {

                Dictionary<string, object> feature = new Dictionary<string, object>();
                Dictionary<string, object> properties = new Dictionary<string,object>();
                Dictionary<string, object> geometry = new Dictionary<string, object>();
                feature["type"] = "Feature";
                feature["id"] = irecord;
                feature["properties"] = properties;     // 属性
                feature["geometry"] = geometry;         // 图形
                features.Add(feature);                  // 添加到要素列表

                // 填写属性
                for (int ifield = 0; ifield < hDBF.GetFieldCount(); ifield++) {
                    FT eType = (FT)fieldInfos[ifield]["type"];
                    switch (eType) {
                        case FT.String: {
                                string value = hDBF.ReadStringAttribute(irecord, ifield);
                                properties[fieldInfos[ifield]["name"].ToString()] = value.Trim();
                            }
                            break;
                        case FT.Integer: {
                                int value = hDBF.ReadIntegerAttribute(irecord, ifield);
                                properties[fieldInfos[ifield]["name"].ToString()] = value;
                            }
                            break;
                        case FT.Double: {
                                double value = hDBF.ReadDoubleAttribute(irecord, ifield);
                                properties[fieldInfos[ifield]["name"].ToString()] = value;
                            }
                            break;
                        case FT.Logical:
                        case FT.Invalid:
                            break;
                        default:
                            break;
                    }

                }

                // 填写图形
                List<object> coordinates = new List<object>();


                geometry["type"] = "";                  // 
                geometry["coordinates"] = coordinates;  // 坐标集合

                SHPObject psShape = hSHP.ReadObject(irecord);
                //
                switch (psShape.nSHPType) {
                    case SHPT.POINT:
                    case SHPT.POINTZ:
                    case SHPT.POINTM:
                        // 点数据
                        geometry["type"] = "Point";
                        break;
                    case SHPT.ARC:
                    case SHPT.ARCZ:
                    case SHPT.ARCM:
                        // 线数据
                        geometry["type"] = "LineString";
                        break;
                    case SHPT.POLYGON:
                    case SHPT.POLYGONZ:
                    case SHPT.POLYGONM:
                        // 面数据
                        geometry["type"] = "Polygon";
                        break;
                    default:
                        MessageBox.Show("暂时无法处理多点/多线数据");
                        throw new Exception();
                }
                //
                if (psShape.nSHPType == SHPT.POINT ||
                    psShape.nSHPType == SHPT.POINTM ||
                    psShape.nSHPType == SHPT.POINTZ) {
                    geometry["coordinates"] = new double[] { psShape.padfX[0], psShape.padfY[0] };
                }
                else {
                    // 读取所有节点
                    for (int index = 0; index < psShape.panPartStart.Length; index++) {
                        if (index < psShape.panPartStart.Length - 1) {
                            // 普通的有开始有结束的
                            int start = psShape.panPartStart[index];
                            int end = psShape.panPartStart[index + 1];

                            List<object> parts = new List<object>();    // 坐标段
                            coordinates.Add(parts);
                            for (int n = start; n < end; n++) {
                                double x = Math.Round(psShape.padfX[n], 6);
                                double y = Math.Round(psShape.padfY[n], 6);
                                parts.Add(new List<object>() { x, y });
                            }

                        }
                        else {
                            // 只有开始无结束的
                            int start = psShape.panPartStart[index];
                            int end = psShape.nVertices;

                            List<object> parts = new List<object>();    // 坐标段
                            coordinates.Add(parts);
                            for (int n = start; n < end; n++) {
                                double x = Math.Round(psShape.padfX[n], 6);
                                double y = Math.Round(psShape.padfY[n], 6);
                                parts.Add(new List<object>() { x, y });
                            }
                        
                        }

                    }
                }

                // 

                string feature_str = Json.JsonSerialize(feature);
                writer.Write(feature_str + ",");

            }
            writer.Write("]}");
            writer.Close();

            //string jsonstr = Json.JsonSerialize(featureCollection);
            //richTextBox.Text = jsonstr;

            richTextBox.Text = "完成.";
        }



    }
}
