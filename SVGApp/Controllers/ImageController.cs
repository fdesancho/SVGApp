using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using Svg;

namespace SVGApp.Controllers
{
    public class ImageController : ApiController
    {
        public HttpResponseMessage Post(double? minDiag=null)
        {            
            try
            {
                if (HttpContext.Current.Request.Files.Count < 1)
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "You must provide a SVG file");

                HttpPostedFile postedFile = HttpContext.Current.Request.Files[0];
                string fileName = Path.GetFileName(postedFile.FileName);
                var ext = fileName.Substring(postedFile.FileName.LastIndexOf('.')+1).ToLower();

                if (ext != "svg")
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "File extension must be .SVG");

                string path = GetUploadPath();
                string filePath = path + fileName;
                postedFile.SaveAs(filePath);

                var resp = ProcessSVG(filePath, minDiag);
                return Request.CreateResponse(HttpStatusCode.OK, resp);
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "There has been an error processing the SVG Document");
            }

           
        }

        private string ProcessSVG(string inputPath, double? minDiag)
        {
            var svgDocument = SvgDocument.Open(inputPath);

            ChangeAlpha(svgDocument.Children, 128);

            ChangePathSize(svgDocument.Children, (float)0.5);

            if(minDiag.HasValue) DeleteSmallerPath(svgDocument.Children, minDiag.Value);

            return ConvertToBase64Bmp(svgDocument);
        }

        private void ChangeAlpha(SvgElementCollection elementList, int alpha)
        {           
                        
            if (elementList.Count > 0)
            {
                foreach (var element in elementList)
                {
                    if (element.Fill != SvgPaintServer.None && element.Fill != null && element.Fill is SvgColourServer)
                        element.Fill = ChangeAlphaColor((element.Fill as SvgColourServer), alpha);

                    if (element.Color != SvgPaintServer.None && element.Color != null && element.Color is SvgColourServer)
                        element.Color = ChangeAlphaColor((element.Color as SvgColourServer), alpha);

                    if (element.Stroke != SvgPaintServer.None && element.Stroke != null && element.Stroke is SvgColourServer)
                        element.Stroke = ChangeAlphaColor((element.Stroke as SvgColourServer), alpha);

                    ChangeAlpha(element.Children,alpha);
                }
            }

        }

        private SvgColourServer ChangeAlphaColor(SvgColourServer color,int alpha)
        {            
            var replaceColor = Color.FromArgb(alpha, color.Colour);
            return new SvgColourServer(replaceColor);
        }

        private void ChangePathSize(SvgElementCollection elementList, float r)
        {
            if (elementList.Count > 0)
            {
                foreach (var element in elementList)
                {
                    if (element is SvgPath)
                    {
                        if (element.Transforms == null)
                            element.Transforms = new Svg.Transforms.SvgTransformCollection();

                        element.Transforms.Add(new Svg.Transforms.SvgScale(r));
                    }

                    ChangePathSize(element.Children, r);
                }
            }

        }

        private void DeleteSmallerPath(SvgElementCollection elementList, double minDiag)
        {           
            if (elementList.Count > 0)
            {
                var elementsToDelete = new List<SvgElement>();
                
                foreach (var item in elementList)
                {
                    if (item is SvgPath && CheckSize(item, minDiag)) elementsToDelete.Add(item) ;
                    else DeleteSmallerPath(item.Children, minDiag);
                }

                foreach (var i in elementsToDelete)
                {
                    elementList.Remove(i);
                }
                    
            }

        }

        private bool CheckSize(SvgElement element, double minDiag)
        {
            var rect = (element as SvgPath).Bounds;
            var d = Math.Sqrt(Math.Pow(rect.Width, 2) + Math.Pow(rect.Height, 2));
            return d < minDiag;
        }

        private string ConvertToBase64Bmp(SvgDocument svgDocument)
        {
            string base64String;

            // Resize the SVG to avoid huge bitmaps on the response
            if(svgDocument.Width > 1024)
            {
                var oldWidth = svgDocument.Width;
                svgDocument.Width = 1024;
                svgDocument.Height *= (1024 / oldWidth);
            }
                        
            using (var bitmap = svgDocument.Draw())
            {
                using (var nonTransBitmap = new Bitmap(bitmap.Width, bitmap.Height))
                {
                    // Force white background for transparent images
                    nonTransBitmap.SetResolution(bitmap.HorizontalResolution, bitmap.VerticalResolution);
                    using (var g = Graphics.FromImage(nonTransBitmap))
                    {
                        g.Clear(Color.White);
                        g.DrawImageUnscaled(bitmap, 0, 0);
                    }

                    // Convert image to stream and encode
                    MemoryStream stream = new System.IO.MemoryStream();
                    nonTransBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                    byte[] imageBytes = stream.ToArray();
                    base64String = Convert.ToBase64String(imageBytes);
                }

            }

            return base64String;
        }

        private string GetUploadPath()
        {
            string path = HttpContext.Current.Server.MapPath("~/Uploads/");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

    }
}
