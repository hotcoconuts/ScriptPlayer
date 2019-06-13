﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScriptPlayer.Shared
{
    public class PositionBar : Control
    {
        public static readonly DependencyProperty MinCommandDelayProperty = DependencyProperty.Register(
            "MinCommandDelay", typeof(TimeSpan), typeof(PositionBar), new PropertyMetadata(TimeSpan.FromMilliseconds(150), OnVisualPropertyChanged));

        public TimeSpan MinCommandDelay
        {
            get => (TimeSpan) GetValue(MinCommandDelayProperty);
            set => SetValue(MinCommandDelayProperty, value);
        }

        public static readonly DependencyProperty LineColorProperty = DependencyProperty.Register(
            "LineColor", typeof(Color), typeof(PositionBar), new PropertyMetadata(Colors.Lime, OnVisualPropertyChanged));

        public Color LineColor
        {
            get => (Color)GetValue(LineColorProperty);
            set => SetValue(LineColorProperty, value);
        }

        public static readonly DependencyProperty LineWidthProperty = DependencyProperty.Register(
            "LineWidth", typeof(double), typeof(PositionBar), new PropertyMetadata(4.0d, OnVisualPropertyChanged));

        public double LineWidth
        {
            get => (double)GetValue(LineWidthProperty);
            set => SetValue(LineWidthProperty, value);
        }

        public static readonly DependencyProperty PositionsProperty = DependencyProperty.Register(
            "Positions", typeof(PositionCollection), typeof(PositionBar), new PropertyMetadata(default(PositionCollection), OnVisualPropertyChanged));

        public PositionCollection Positions
        {
            get => (PositionCollection)GetValue(PositionsProperty);
            set => SetValue(PositionsProperty, value);
        }

        public static readonly DependencyProperty TotalDisplayedDurationProperty = DependencyProperty.Register(
            "TotalDisplayedDuration", typeof(TimeSpan), typeof(PositionBar), new PropertyMetadata(TimeSpan.FromSeconds(5), OnVisualPropertyChanged));

        public TimeSpan TotalDisplayedDuration
        {
            get => (TimeSpan)GetValue(TotalDisplayedDurationProperty);
            set => SetValue(TotalDisplayedDurationProperty, value);
        }

        public static readonly DependencyProperty MidpointProperty = DependencyProperty.Register(
            "Midpoint", typeof(double), typeof(PositionBar), new PropertyMetadata(0.5d, OnVisualPropertyChanged));

        public double Midpoint
        {
            get => (double)GetValue(MidpointProperty);
            set => SetValue(MidpointProperty, value);
        }

        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            "Progress", typeof(TimeSpan), typeof(PositionBar), new PropertyMetadata(default(TimeSpan), OnVisualPropertyChanged));

        private bool _down;
        private Point _mousePos;
        private TimedPosition _position;

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PositionBar)d).InvalidateVisual();
        }

        public TimeSpan Progress
        {
            get => (TimeSpan)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
            "IsReadOnly", typeof(bool), typeof(PositionBar), new PropertyMetadata(default(bool)));

        public bool IsReadOnly
        {
            get => (bool) GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (IsReadOnly)
                return;

            _mousePos = e.GetPosition(this);

            // Control = Remove
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                TimedPosition closest = GetClosestPosition(_mousePos);
                if (closest != null)
                    Positions.Remove(closest);

                InvalidateVisual();
            }
            // Shift = Add
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                TimedPosition newPos = GetPositionFromPoint(_mousePos);
                Positions.Add(newPos);

                _position = newPos;
                _down = true;

                CaptureMouse();

                InvalidateVisual();
            }
            // Neither = Move
            else
            {
                TimedPosition closest = GetClosestPosition(_mousePos);
                if (closest == null)
                    return;

                _position = closest;
                _down = true;

                CaptureMouse();
            }
        }

        private TimedPosition GetClosestPosition(Point mousePos)
        {
            TimeSpan timeFrom = Progress - TotalDisplayedDuration.Multiply(Midpoint);
            TimeSpan timeTo = Progress + TotalDisplayedDuration.Multiply(1 - Midpoint);

            List<TimedPosition> absoluteBeatPositions = Positions.GetPositions(timeFrom, timeTo).ToList();

            double minDist = double.MaxValue;
            TimedPosition closest = null;

            foreach (TimedPosition position in absoluteBeatPositions)
            {
                double distance = GetPointFromPosition(position).DistanceTo(mousePos);
                if (distance < minDist)
                {
                    closest = position;
                    minDist = distance;
                }
            }

            if (closest == null)
                return null;

            if (minDist > 20) return null;

            return closest;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!_down) return;

            if (IsReadOnly)
                return;

            _down = false;
            _mousePos = e.GetPosition(this);
            UpdateSelectedPosition(true);
            InvalidateVisual();

            ReleaseMouseCapture();
        }

        private void UpdateSelectedPosition(bool final)
        {
            if (final)
            {
                Positions.Remove(_position);
                Positions.Add(GetPositionFromPoint(_mousePos));
            }
            else
            {
                TimedPosition pos = GetPositionFromPoint(_mousePos);
                _position.TimeStamp = pos.TimeStamp;
                _position.Position = pos.Position;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_down) return;

            if (IsReadOnly)
                return;

            _mousePos = e.GetPosition(this);
            UpdateSelectedPosition(false);
            InvalidateVisual();
        }

        private int GetPositionAtTime(TimeSpan time)
        {
            if (Positions.Count == 0)
                return 50;

            TimeSpan seek = TimeSpan.Zero;
            int i;
            for (i = 0; i < Positions.Count; i++)
            {
                seek = Positions[i].TimeStamp;
                if (seek > time)
                    break;
                else if (i == Positions.Count - 1)
                    return Positions[i].Position;
            }

            if (i == 0)
                return Positions[i].Position;

            double lerper = ((time - Positions[i - 1].TimeStamp).TotalSeconds / (Positions[i].TimeStamp - Positions[i - 1].TimeStamp).TotalSeconds);

            return (int)(Positions[i - 1].Position + lerper * (Positions[i].Position - Positions[i-1].Position));
        }

        private Point GetPointFromPosition(TimedPosition position)
        {
            double x = PositionToX(position.TimeStamp);
            double y = PositionToY(position.Position);

            return new Point(x,y);
        }

        private double PositionToX(TimeSpan timeStamp)
        {
            return (timeStamp - (Progress - TotalDisplayedDuration.Multiply(Midpoint))).Divide(TotalDisplayedDuration) * ActualWidth;
        }

        private double PositionToY(int position)
        {
            return (ActualHeight - 2 * CircleRadius) * ((99.0 - position) / 99.0) + CircleRadius;
        }

        private TimedPosition GetPositionFromPoint(Point point)
        {
            TimeSpan timestamp = XToPosition(point.X);
            byte position = YToPosition(point.Y);
            position = Math.Min((byte)99, Math.Max((byte)0, position));

            return new TimedPosition
            {
                TimeStamp = timestamp,
                Position = position
            };
        }

        private TimeSpan XToPosition(double x)
        {
            return TotalDisplayedDuration.Multiply(x / ActualWidth) + (Progress - TotalDisplayedDuration.Multiply(Midpoint));
        }

        private byte YToPosition(double y)
        {
            double usableHeight = ActualHeight - 2 * CircleRadius;
            return (byte)(Math.Min(usableHeight, Math.Max(0, usableHeight - (y - CircleRadius))) / usableHeight * 99.0);
        }

        private static double CircleRadius = 4;

        private Point Offset;

        private Point AddPoints(Point a, Point b) { return new Point(a.X + b.X, a.Y + b.Y); }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var y = PositionToY(GetPositionAtTime(Progress));
            var clampedY = Math.Min(ActualHeight, Math.Max(0, y));
            Offset = new Point(0, clampedY - y);

            if (_down)
            {
                UpdateSelectedPosition(false);
            }

            Pen redPen = new Pen(Brushes.Red, 1);
            Rect fullRect = new Rect(new Point(), new Size(ActualWidth, ActualHeight));

            drawingContext.PushClip(new RectangleGeometry(fullRect));

            drawingContext.DrawRectangle(Background, null, fullRect);

            drawingContext.PushOpacity(0.3);
            for (int i = 0; i <= 4; i++)
            {
                var yoffset = (Offset.Y % (ActualHeight / 4.0));
                drawingContext.DrawLine(redPen, new Point(0, i * ActualHeight / 4.0 + yoffset), new Point(ActualWidth, i * ActualHeight / 4.0 + yoffset));
            }
            drawingContext.Pop();

            double midPointX = ActualWidth * Midpoint;

            drawingContext.DrawLine(redPen, new Point(midPointX, 0), new Point(midPointX, ActualHeight));

            if (Positions != null)
            {
                TimeSpan timeFrom = Progress - TotalDisplayedDuration.Multiply(Midpoint);
                TimeSpan timeTo = Progress + TotalDisplayedDuration.Multiply(1 - Midpoint);

                List<TimedPosition> absoluteBeatPositions = Positions.GetPositions(timeFrom, timeTo).ToList();
                //double ToLocal(TimedPosition b) => (b.TimeStamp - timeFrom).Divide(timeTo - timeFrom) * ActualWidth;

                List<Point> beatPoints = absoluteBeatPositions.Select(GetPointFromPosition).ToList();

                if (beatPoints.Count > 0)
                {
                    /*
                    PathFigure figure = new PathFigure {StartPoint = beatPoints[0]};

                    for (int i = 1; i < beatPoints.Count; i++)
                    {
                        figure.Segments.Add(new LineSegment(beatPoints[i], true));
                    }

                    drawingContext.DrawGeometry(null, redPen, new PathGeometry(new[] {figure}));
                    */

                    for (int i = 1; i < beatPoints.Count; i++)
                    {
                        TimeSpan duration = absoluteBeatPositions[i].TimeStamp - absoluteBeatPositions[i - 1].TimeStamp;

                        double speed = SpeedPredictor.PredictSpeed2(absoluteBeatPositions[i - 1].Position, absoluteBeatPositions[i].Position, duration) / 99.0;

                        Color color = HeatMapGenerator.GetColorAtPosition(HeatMapGenerator.HeatMap, speed);
                        drawingContext.DrawLine(new Pen(new SolidColorBrush(color), 1), AddPoints(beatPoints[i-1], Offset), AddPoints(beatPoints[i], Offset) );
                    }
                    

                    for (int i = 0; i < beatPoints.Count; i++)
                    {
                        Color color;

                        if (i == 0)
                            color = Colors.Lime;
                        else
                        {
                            TimeSpan duration = absoluteBeatPositions[i].TimeStamp - absoluteBeatPositions[i - 1].TimeStamp;
                            double durationFactor = MinCommandDelay.Divide(duration);
                            color = HeatMapGenerator.GetColorAtPosition(HeatMapGenerator.HeatMap3, durationFactor);
                        }
                        Color fillColor = HeatMapGenerator.MixColors(color, Colors.Black, 0.5);

                        drawingContext.DrawEllipse(new SolidColorBrush(fillColor), new Pen(new SolidColorBrush(color), 1), AddPoints(beatPoints[i], Offset), CircleRadius, CircleRadius);   
                    }
                }
            }

            drawingContext.Pop();
        }
    }
}
