using System.Windows;
using System.Windows.Media;

namespace RaceNotifier.UI
{
    /// <summary>
    /// The plugin's left-menu (sidebar) icon: a white notification/chat bubble with three
    /// message dots, drawn as a vector <see cref="DrawingImage"/> so it stays crisp at any
    /// size/DPI and needs no embedded bitmap. White monochrome matches SimHub's built-in
    /// menu icons; the three dots are punched out as holes so they read against whatever
    /// background the active SimHub theme uses.
    /// </summary>
    internal static class PluginIcon
    {
        /// <summary>Shared, frozen icon instance returned from the plugin's PictureIcon.</summary>
        public static readonly ImageSource Default = Build();

        private static ImageSource Build()
        {
            // 24x24 coordinate space, matching SimHub's left-menu icon size.

            // Rounded bubble body.
            var body = new RectangleGeometry(new Rect(2, 3, 20, 13), 3.5, 3.5);

            // Small tail at the bottom-left; overlaps the body so the union is seamless.
            var tailFigure = new PathFigure { StartPoint = new Point(6, 14), IsClosed = true };
            tailFigure.Segments.Add(new LineSegment(new Point(6, 21), true));
            tailFigure.Segments.Add(new LineSegment(new Point(11.5, 14), true));
            var tail = new PathGeometry(new[] { tailFigure });

            var bubble = new CombinedGeometry(GeometryCombineMode.Union, body, tail);

            // Three message dots, subtracted from the bubble so they appear as holes.
            var dots = new GeometryGroup();
            dots.Children.Add(new EllipseGeometry(new Point(8, 9.5), 1.7, 1.7));
            dots.Children.Add(new EllipseGeometry(new Point(12, 9.5), 1.7, 1.7));
            dots.Children.Add(new EllipseGeometry(new Point(16, 9.5), 1.7, 1.7));

            var withHoles = new CombinedGeometry(GeometryCombineMode.Exclude, bubble, dots);

            var white = new SolidColorBrush(Colors.White);
            white.Freeze();

            var drawing = new GeometryDrawing(white, null, withHoles);
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }
    }
}
