﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.IO;

using cgshop.point;
using System.ComponentModel;
using System.Runtime.CompilerServices;


namespace cgshop
{

    public enum ShapeType
    {
        Line,
        Circle,
        Poly
    }

    public partial class VectorEditing : Page, INotifyPropertyChanged
    {
        private int FINISH_POLY_THRESHOLD = 15;
        private int DRAGGING_POINT_SIZE = 8;
        private int PROJECTION_POINT_SIZE = 5;
       // private int MINIMAL_DRAGGING_POINT_MARGIN = 8;

        private Drawer drawer;

        private List<Point> previousClickPoints;


        private ShapeType selectedShapeType;
        private Shape selectedShape;

        Point draggingStartPoint;
        bool drag = false;

        private bool drawing;
        public bool Drawing { get { return drawing; } set { drawing = value; OnPropertyChanged(); } }
        private List<Ellipse> activeDraggingPoints; // For altering the shapes
        private List<Ellipse> activeProjectionPoints; // For seeing points when drawing new shape
        private Ellipse activeDraggingPoint;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        public VectorEditing()
        {
            InitializeComponent();
            DataContext = this;
            SetupModule();
        }

        private void SetupModule()
        {
            activeDraggingPoints = new List<Ellipse>();
            activeProjectionPoints = new List<Ellipse>();
            previousClickPoints = new List<Point>();

            var canvas = new BitmapImage(new Uri("/Res/Test.png", UriKind.Relative));

            /*
            int width = (int)128;
            int height = (int)128;

            int bytesPerPixel = PixelFormats.Bgra32.BitsPerPixel; // 32
            int stride = width * bytesPerPixel / 8; // Width * 4;
            byte[] pixels = new byte[height * stride];
            //BitmapPalette bitmapPalette = new BitmapPalette(new List<System.Windows.Media.Color>() { Colors.Green });
            BitmapPalette bitmapPalette = new BitmapPalette(new List<System.Windows.Media.Color>() { Colors.Blue, Colors.Green, Colors.Red, Colors.Transparent });
            BitmapSource bitmapSource = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, bitmapPalette, pixels, stride);

            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream stream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(stream);
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }
            */

            drawer = new Drawer(canvas);
            CanvasImage.Source = drawer.canvas;

            ShapeSelector.ItemsSource = Enum.GetValues(typeof(ShapeType));
            ShapeSelector.SelectedIndex = 0;
            

            Line l1 = new Line("line", new Point(10,10), new Point(25,25), 1, new Color(255,0,0,255));
            Line l2 = new Line("line" ,new Point(100, 100), new Point(250, 250), 1, new Color(0, 0, 255, 255));
            l1.name = "anui";
            drawer.AddShape(l1);
            drawer.AddShape(l2);
            ShapeList.ItemsSource = drawer.shapes;
            CanvasImage.Source = drawer.RedrawCanvas();
        }

        private void CanvasImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Drawing)
            {
                Point clickPoint = new Point(e.GetPosition(sender as System.Windows.IInputElement));
                switch (selectedShapeType)
                {

                    case (ShapeType.Line):
                    case (ShapeType.Circle):

                        //if (previousClickPoints[0] != null)
                        if (previousClickPoints.Count >= 1)
                        {
                            Point p1 = previousClickPoints[0];
                            Point p2 = clickPoint;

                            Shape newShape;
                            switch (selectedShapeType)
                            {
                                case ShapeType.Line:
                                    newShape = new Line(selectedShapeType.ToString(), p1, p2, 1, new Color(0, 0, 0, 255));
                                    break;

                                case ShapeType.Circle:
                                    newShape = new Circle(selectedShapeType.ToString(), p1, p2, new Color(0, 0, 0, 255));
                                    break;

                                default:
                                    throw new NotImplementedException();
                            }

                            CanvasImage.Source = drawer.AddShape(newShape);
                            ShapeList.SelectedIndex = ShapeList.Items.Count - 1;

                            StopDrawing();
                        }
                        else
                        {
                            previousClickPoints.Add(clickPoint);
                            activeProjectionPoints.Add(CreateProjectionPoint(clickPoint));
                        }
                        break;

                    case (ShapeType.Poly):

                        //if (previousClickPoints[0] != null && previousClickPoints[1] != null)
                       
                        if (previousClickPoints.Count >= 2)
                        {
                            if (clickPoint.Distance(previousClickPoints[0]) < (double)FINISH_POLY_THRESHOLD) { // If is new point is near the first placed point then create polygon
                                Shape newShape = new Poly(selectedShapeType.ToString(), new List<Point>(previousClickPoints), 1, new Color(0, 0, 0, 255));
                                CanvasImage.Source = drawer.AddShape(newShape);

                                ShapeList.SelectedIndex = ShapeList.Items.Count - 1;
                                StopDrawing();
                            }
                            else
                            {
                                previousClickPoints.Add(clickPoint);
                                activeProjectionPoints.Add(CreateProjectionPoint(clickPoint));
                            }
                        }
                        else
                        {
                            previousClickPoints.Add(clickPoint);
                            activeProjectionPoints.Add(CreateProjectionPoint(clickPoint));
                        }

                    break;

                    default:
                        throw new NotImplementedException();       
                }
            }
            else
            {
                DeselectShape();
            }
           
        }

     

        private void DeselectShape()
        {
            ShapeList.SelectedIndex = -1;
            selectedShape = null;
            for (int p = activeDraggingPoints.Count - 1; p >= 0; p--)
            {
                CanvasUI.Children.Remove(activeDraggingPoints[p]);
                activeDraggingPoints.Remove(activeDraggingPoints[p]);
            }    
        }

        private void ShapeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (drawer.shapes.Count > 0 && ShapeList.SelectedIndex >= 0)
            {          
                StopDrawing();
                SelectShape();
            }
            else
            {
                selectedShape = null;
            }
        }

        private void Page_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete) // Removing dragging point
            {
                if (selectedShape != null)
                {
                    drag = false;
                    draggingStartPoint = null;
                    previousClickPoints.Clear();
                    selectedShape = null;
                    drawer.shapes.RemoveAt(ShapeList.SelectedIndex);
                    CanvasImage.Source = drawer.RedrawCanvas();

                    activeDraggingPoint = null;

                    for (int i = activeDraggingPoints.Count - 1; i >= 0; i--)
                    {
                        CanvasUI.Children.RemoveAt(i);
                        activeDraggingPoints.RemoveAt(i);
                    }
                }
            }
        }

        private void SelectShape()
        {
            // Remove active dragging points
            for (int p = activeDraggingPoints.Count - 1; p >= 0; p--)
            {
                CanvasUI.Children.Remove(activeDraggingPoints[p]);
                activeDraggingPoints.Remove(activeDraggingPoints[p]);
            }

            selectedShape = drawer.shapes[ShapeList.SelectedIndex];

            // Update shape settings menu
            
            if (selectedShape.GetType() == typeof(Line))
            {
                ShapeColorSettingTab.Visibility = System.Windows.Visibility.Visible;
                ShapeThicknessSettingTab.Visibility = System.Windows.Visibility.Visible;
                ShapeAntialiasingSettingTab.Visibility = System.Windows.Visibility.Visible;

                // Sync shape settings
                ShapeThicknessSetting.Value = (selectedShape as Line).Thickness;
                Color c = (selectedShape as Line).Color;
                ShapeColorSetting.SelectedColor = System.Windows.Media.Color.FromArgb(c[3], c[2], c[1], c[0]);
                ShapeAntialiasingSetting.IsChecked = (selectedShape as Line).Antialiased;

            }
            else if (selectedShape.GetType() == typeof(Circle))
            {
                ShapeColorSettingTab.Visibility = System.Windows.Visibility.Visible;
                ShapeThicknessSettingTab.Visibility = System.Windows.Visibility.Hidden;
                ShapeAntialiasingSettingTab.Visibility = System.Windows.Visibility.Hidden;

                // Sync shape settings
                Color c = (selectedShape as Circle).Color;
                ShapeColorSetting.SelectedColor = System.Windows.Media.Color.FromArgb(c[3], c[2], c[1], c[0]);
            }
            else if (selectedShape.GetType() == typeof(Poly))
            {
                ShapeColorSettingTab.Visibility = System.Windows.Visibility.Visible;
                ShapeThicknessSettingTab.Visibility = System.Windows.Visibility.Visible;
                ShapeAntialiasingSettingTab.Visibility = System.Windows.Visibility.Visible;

                // Sync shape settings
                ShapeThicknessSetting.Value = (selectedShape as Poly).Thickness;
                Color c = (selectedShape as Poly).Color;
                ShapeColorSetting.SelectedColor = System.Windows.Media.Color.FromArgb(c[3], c[2], c[1], c[0]);
                ShapeAntialiasingSetting.IsChecked = (selectedShape as Poly).Antialiased;
            }


            // Add new dragging points
            for (int p = 0; p < selectedShape.GetPoints().Count; p++)
            { 
                CreateDraggingPoint(p);
            }

            Ellipse CreateDraggingPoint(int p)
            {
                Point point = selectedShape.GetPoints()[p];

                Ellipse draggingPoint = new Ellipse() { };
                draggingPoint.Width = DRAGGING_POINT_SIZE;
                draggingPoint.Height = DRAGGING_POINT_SIZE;
                draggingPoint.Cursor = Cursors.Hand;
                draggingPoint.Fill = new SolidColorBrush(Colors.Black);
                draggingPoint.Fill.Opacity = 0;
                draggingPoint.Stroke = new SolidColorBrush(Colors.Black);

                Point canvasPoint = CalculateCanvasPositionFromPointValue(point);
                Canvas.SetLeft(draggingPoint, canvasPoint.X);
                Canvas.SetTop(draggingPoint, canvasPoint.Y);
                Panel.SetZIndex(draggingPoint, 5); // Higher means top

                draggingPoint.MouseLeftButtonDown += (senderPoint, argsPoint) =>
                {
                    activeDraggingPoint = draggingPoint;
                    draggingPoint.Fill.Opacity = 1;

                    drag = true; // start dragging
                    draggingStartPoint = new Point(Mouse.GetPosition(CanvasUI)); // save start point of dragging
                };
                draggingPoint.MouseMove += (senderPoint, argsPoint) =>
                {
                    // if dragging, then adjust rectangle position based on mouse movement
                    if (drag)
                    {
                        Ellipse draggedItem = senderPoint as Ellipse;
                        int draggedItemIndex = activeDraggingPoints.IndexOf(senderPoint as Ellipse);

                        Point newPoint = new Point(Mouse.GetPosition(CanvasUI));

                        // Restrain dragging
                        if (newPoint.Y < 0) { newPoint.Y = 0; }
                        if (newPoint.Y > CanvasUI.ActualHeight + DRAGGING_POINT_SIZE / 2) { newPoint.Y = (int)(CanvasUI.ActualHeight + DRAGGING_POINT_SIZE / 2); }

                        double left = Canvas.GetLeft(draggedItem);
                        double top = Canvas.GetTop(draggedItem);
                        double newLeft = left + (newPoint.X - draggingStartPoint.X);

                        double newTop = top + (newPoint.Y - draggingStartPoint.Y);
                        Canvas.SetLeft(draggedItem, newLeft);
                        Canvas.SetTop(draggedItem, newTop);

                        draggingStartPoint = newPoint;

                    }
                };
                draggingPoint.MouseLeftButtonUp += (senderPoint, argsPoint) =>
                {
                    StopDraggingPoint(senderPoint, selectedShape);
                };
                draggingPoint.MouseLeave += (senderPoint, argsPoint) =>
                {
                    StopDraggingPoint(senderPoint, selectedShape);
                };

                CanvasUI.Children.Insert(p, draggingPoint); // Draw points on canvas
                activeDraggingPoints.Insert(p, draggingPoint);

                return draggingPoint;
            }
        }

        void StopDraggingPoint(object point, Shape shape)
        {
            if (drag)
            {
                // stop dragging
                drag = false;

                Ellipse draggedItem = point as Ellipse;
                int draggedItemIndex = activeDraggingPoints.IndexOf(point as Ellipse);

                Point position = new Point(Canvas.GetLeft(draggedItem), Canvas.GetTop(draggedItem));

                // Ensure restrictions
                if (position.Y - DRAGGING_POINT_SIZE / 2 < 0) { position.Y = 0 - DRAGGING_POINT_SIZE / 2; }

                Canvas.SetLeft(draggedItem, position.X);
                Canvas.SetTop(draggedItem, position.Y);

                // Set new value to point
                Point newPoint = CalculatePointValueFromCanvasPosition(new Point(Canvas.GetLeft(draggedItem), Canvas.GetTop(draggedItem))); ;
                selectedShape.GetPoints()[draggedItemIndex].X = newPoint.X;
                selectedShape.GetPoints()[draggedItemIndex].Y = newPoint.Y;

                CanvasImage.Source = drawer.RedrawCanvas();
            }
        }

        Point CalculateCanvasPositionFromPointValue(Point point)
        {
            double x = point.X - DRAGGING_POINT_SIZE / 2;
            double y = point.Y - DRAGGING_POINT_SIZE / 2;
            return new Point(x, y);
        }

        Point CalculatePointValueFromCanvasPosition(Point position)
        {
            double x = position.X + DRAGGING_POINT_SIZE / 2;
            double y = position.Y + DRAGGING_POINT_SIZE / 2;
            return new Point(x, y);
        }

        private void CanvasUI_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DeselectActiveDraggingPoint();
        }

        private void DeselectActiveDraggingPoint()
        {
            // Deselect currently selected point
            if (activeDraggingPoint != null)
            {
                activeDraggingPoint.Fill.Opacity = 0;
                activeDraggingPoint = null;
            }
        }

        private Ellipse CreateProjectionPoint(Point point)
        {
            Ellipse projectionPoint = new Ellipse() { };
            projectionPoint.Width = PROJECTION_POINT_SIZE;
            projectionPoint.Height = PROJECTION_POINT_SIZE;
            projectionPoint.Fill = new SolidColorBrush(Colors.Gray);

            Point canvasPoint = CalculateCanvasPositionFromPointValue(point);
            Canvas.SetLeft(projectionPoint, canvasPoint.X);
            Canvas.SetTop(projectionPoint, canvasPoint.Y);
            Panel.SetZIndex(projectionPoint, 6); // Higher means top

            CanvasUI.Children.Add(projectionPoint); // Draw points on canvas

            return projectionPoint;
        }

        private void StopDrawing()
        {
            foreach (var projectionPoint in activeProjectionPoints)
            {
                CanvasUI.Children.Remove(projectionPoint);
            }
            activeProjectionPoints.Clear();

            Mouse.OverrideCursor = null;
            Drawing = false;
            previousClickPoints.Clear(); 
        }

        private void Canvas_MouseEnter(object sender, MouseEventArgs e)
        {
            if (Drawing)
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Cross;
            }
        }

        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (Drawing)
            {
                Mouse.OverrideCursor = null;
            }
        }

        // Toolbox
        private void CreateButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DeselectShape();
            Drawing = true;
        }

        private void ShapeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StopDrawing();
            selectedShapeType = (ShapeType)(sender as ComboBox).SelectedIndex;
        }

        private void ShapeThicknessSetting_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (selectedShape != null)
            {
                if (selectedShape.GetType() == typeof(Line))
                {
                    (selectedShape as Line).Thickness = (int)ShapeThicknessSetting.Value;
                }
                else if (selectedShape.GetType() == typeof(Poly))
                {
                    (selectedShape as Poly).Thickness = (int)ShapeThicknessSetting.Value;
                }

                CanvasImage.Source = drawer.RedrawCanvas();
            }
        }

        private void ShapeColorSetting_SelectedColorChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (selectedShape != null)
            {

                if (selectedShape.GetType() == typeof(Line))
                {
                    (selectedShape as Line).Color = new Color(ShapeColorSetting.SelectedColor.Value);
                }
                else if (selectedShape.GetType() == typeof(Circle))
                {
                    (selectedShape as Circle).Color = new Color(ShapeColorSetting.SelectedColor.Value);
                }
                else if (selectedShape.GetType() == typeof(Poly))
                {
                    (selectedShape as Poly).Color = new Color(ShapeColorSetting.SelectedColor.Value);
                }

                CanvasImage.Source = drawer.RedrawCanvas();
            }           
        }

        private void ShapeAntialiasingSetting_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (selectedShape.GetType() == typeof(Line))
            {
                (selectedShape as Line).Antialiased = ShapeAntialiasingSetting.IsChecked.Value;
            }
            else if (selectedShape.GetType() == typeof(Poly))
            {
                (selectedShape as Poly).Antialiased = ShapeAntialiasingSetting.IsChecked.Value;
            }

            CanvasImage.Source = drawer.RedrawCanvas();
        }


        // Top buttons
        private void ButtonLoad_Click(object sender, System.Windows.RoutedEventArgs e)
        {
          
        }

        private void ButtonClear_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            selectedShape = null;
            StopDrawing();
            for (int p = activeDraggingPoints.Count - 1; p >= 0; p--)
            {
                CanvasUI.Children.Remove(activeDraggingPoints[p]);
                activeDraggingPoints.Remove(activeDraggingPoints[p]);
            }

            CanvasImage.Source = drawer.Clear();
        }

        private void ButtonSave_Click(object sender, System.Windows.RoutedEventArgs e)
        {
          
        }

       
    }

}
