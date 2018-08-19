using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Obsidian.Preview
{
    public class MouseHandler
    {
        private Vector3D _center;
        private bool _centered; // Have we already determined the rotation center?
        // The state of the trackball
        private bool _enabled;
        private Point _point; // Initial point of drag
        private bool _rotating;
        private Quaternion _rotation;
        private Quaternion _rotationDelta; // Change to rotation because of this drag
        private double _scale;
        private double _scaleDelta; // Change to scale because of this drag
        // The state of the current drag
        private bool _scaling; // Are we scaling?  NOTE otherwise we're rotating
        private List<Viewport3D> _slaves = new List<Viewport3D>();
        private Vector3D _translate;
        private Vector3D _translateDelta;
        public List<Viewport3D> Slaves => this._slaves;
        public bool Enabled
        {
            get => this._enabled && (this._slaves != null) && (this._slaves.Count > 0);
            set => this._enabled = value;
        }

        public MouseHandler()
        {
            Reset();
        }

        public void Attach(FrameworkElement element)
        {
            element.MouseMove += MouseMoveHandler;
            element.MouseRightButtonDown += MouseDownHandler;
            element.MouseRightButtonUp += MouseUpHandler;
            element.MouseWheel += OnMouseWheel;
        }

        public void Detach(FrameworkElement element)
        {
            element.MouseMove -= MouseMoveHandler;
            element.MouseRightButtonDown -= MouseDownHandler;
            element.MouseRightButtonUp -= MouseUpHandler;
            element.MouseWheel -= OnMouseWheel;
        }

        private void UpdateSlaves(Quaternion rotation, double scale, Vector3D translation)
        {
            if (this._slaves != null)
            {
                foreach (Viewport3D slave in this._slaves)
                {
                    ModelVisual3D modelVisual = slave.Children[0] as ModelVisual3D;
                    Transform3DGroup transformGroup = modelVisual.Transform as Transform3DGroup;

                    ScaleTransform3D groupScaleTransform = transformGroup.Children[0] as ScaleTransform3D;
                    RotateTransform3D groupRotateTransform = transformGroup.Children[1] as RotateTransform3D;
                    TranslateTransform3D groupTranslateTransform = transformGroup.Children[2] as TranslateTransform3D;

                    groupScaleTransform.ScaleX = scale;
                    groupScaleTransform.ScaleY = scale;
                    groupScaleTransform.ScaleZ = scale;
                    groupRotateTransform.Rotation = new AxisAngleRotation3D(rotation.Axis, rotation.Angle);
                    groupTranslateTransform.OffsetX = translation.X;
                    groupTranslateTransform.OffsetY = translation.Y;
                    groupTranslateTransform.OffsetZ = translation.Z;
                }
            }
        }

        private void MouseMoveHandler(object sender, MouseEventArgs e)
        {
            if (!this.Enabled)
            {
                return;
            }
            e.Handled = true;

            UIElement senderElement = (UIElement)sender;
            if (senderElement.IsMouseCaptured)
            {
                Vector delta = (this._point - e.MouseDevice.GetPosition(senderElement)) / 2;
                Vector3D translation = new Vector3D();
                Quaternion rotation = this._rotation;

                if (this._rotating)
                {

                    Vector3D mouse = new Vector3D(delta.X, -delta.Y, 0);
                    Vector3D axis = Vector3D.CrossProduct(mouse, new Vector3D(0, 0, 1));
                    double len = axis.Length;
                    if (len < 0.00001 || this._scaling)
                    {
                        this._rotationDelta = new Quaternion(new Vector3D(0, 0, 1), 0);
                    }
                    else
                    {
                        this._rotationDelta = new Quaternion(axis, len);
                    }

                    rotation = this._rotationDelta * this._rotation;
                }
                else
                {
                    delta /= 20;
                    this._translateDelta.X = delta.X * -1;
                    this._translateDelta.Y = delta.Y;
                }

                translation = this._translate + this._translateDelta;

                UpdateSlaves(rotation, this._scale * this._scaleDelta, translation);
            }
        }

        private void MouseDownHandler(object sender, MouseButtonEventArgs e)
        {
            if (!this.Enabled) return;
            e.Handled = true;

            if (Keyboard.IsKeyDown(Key.F1))
            {
                Reset();
                return;
            }

            UIElement senderElement = (UIElement)sender;
            this._point = e.MouseDevice.GetPosition(senderElement);
            if (!this._centered)
            {
                ProjectionCamera camera = (ProjectionCamera)this._slaves[0].Camera;
                this._center = camera.LookDirection;
                this._centered = true;
            }

            this._scaling = (e.MiddleButton == MouseButtonState.Pressed);
            this._rotating = Keyboard.IsKeyDown(Key.Space) == false;

            senderElement.CaptureMouse();
        }

        private void MouseUpHandler(object sender, MouseButtonEventArgs e)
        {
            if (!this._enabled)
            {
                return;
            }

            e.Handled = true;
            if (this._rotating)
            {
                this._rotation *= this._rotationDelta;
            }
            else
            {
                this._translate += this._translateDelta;
                this._translateDelta.X = 0;
                this._translateDelta.Y = 0;
            }

            (sender as UIElement).ReleaseMouseCapture();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            this._scaleDelta += e.Delta / (double)1000;
            Quaternion q = this._rotation;

            UpdateSlaves(q, this._scale * this._scaleDelta, this._translate);
        }

        public void Reset()
        {
            this._rotation = new Quaternion(0, 0, 0, 1);
            this._scale = 1;
            this._translate.X = 0;
            this._translate.Y = 0;
            this._translate.Z = 0;
            this._translateDelta.X = 0;
            this._translateDelta.Y = 0;
            this._translateDelta.Z = 0;
            this._rotationDelta = Quaternion.Identity;
            this._scaleDelta = 1;
            UpdateSlaves(this._rotation, this._scale, this._translate);
        }
    }
}