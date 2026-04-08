#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    public sealed class Cone : IDisposable
    {
        public static Vector3D[] RenderCone(Vector3D baseToApexLength, Vector3D apexLocation, double height, double radius, int slices)
        {
            Vector3D c = apexLocation + (-baseToApexLength * height);
            Vector3D e0 = Perpendicular(baseToApexLength);
            Vector3D e1 = e0.Cross(baseToApexLength);
            double angInc = (360.0 / slices * Math.PI / 180.0);
            slices++; // this was the fix for my problem.
            /**
             * Compute the Vertices around the Directrix
             */
            Vector3D[] vertices = new Vector3D[slices];
            for (int i = 0; i < vertices.Length; ++i)
            {
                double rad = angInc * i;
                Vector3D p = c + (((e0 * Math.Cos((rad)) + (e1 * Math.Sin(rad))) * radius));
                vertices[i] = p;
            }

            return vertices;
        }

        public static List<Vector3D> RenderCone2(Vector3D baseToApexLength, Vector3D apexLocation, double height, double radius, int slices)
        {
            var res = new List<Vector3D>();
            Vector3D c = apexLocation + (-baseToApexLength * height);
            Vector3D e0 = Perpendicular(baseToApexLength);
            Vector3D e1 = e0.Cross(baseToApexLength);
            double angInc = (360.0 / slices * Math.PI / 180.0);
            slices++; // this was the fix for my problem.
            /**
             * Compute the Vertices around the Directrix
             */
            res.Add(apexLocation);
            for (int i = 0; i < slices; ++i)
            {
                double rad = angInc * i;
                Vector3D p = c + (((e0 * Math.Cos((rad)) + (e1 * Math.Sin(rad))) * radius));
                res.Add(p);
            }

            return res;
        }

        public static Vector3D Perpendicular(Vector3D v)
        {
            double min = Math.Abs(v.X);
            Vector3D cardinalAxis = new Vector3D(1, 0, 0);
            if (Math.Abs(v.Y) < min)
            {
                min = Math.Abs(v.Y);
                cardinalAxis = new Vector3D(0, 1, 0);
            }
            if (Math.Abs(v.Z) < min)
            {
                cardinalAxis = new Vector3D(0, 0, 1);
            }
            return v.Cross(cardinalAxis);
        }

        public Cone(Context context, int slices)
        {
            Verify.ThrowIfNull(context);

            RenderState lineRS = new RenderState();
            lineRS.FacetCulling.Enabled = false;

            ShaderProgram lineSP = Device.CreateShaderProgram(
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.LineVS.glsl"),
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.LineGS.glsl"),
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.LineFS.glsl"));
            _lineLogarithmicDepth = (Uniform<bool>)lineSP.Uniforms["u_logarithmicDepth"];
            _lineLogarithmicDepthConstant = (Uniform<float>)lineSP.Uniforms["u_logarithmicDepthConstant"];
            _lineFillDistance = (Uniform<float>)lineSP.Uniforms["u_fillDistance"];
            _lineColorUniform = (Uniform<Vector3F>)lineSP.Uniforms["u_color"];

            OutlineWidth = 1;
            OutlineColor = Color.Gray;

            ///////////////////////////////////////////////////////////////////

            RenderState fillRS = new RenderState();
            // Test options
            fillRS.FacetCulling.Enabled = true;
            //fillRS.FacetCulling.FrontFaceWindingOrder = WindingOrder.Clockwise;
            fillRS.FacetCulling.Face = CullFace.Front;
            fillRS.FacetCulling.FrontFaceWindingOrder = WindingOrder.Counterclockwise;
            fillRS.DepthTest.Function = DepthTestFunction.LessThanOrEqual;
            //

            fillRS.Blending.Enabled = true;
            fillRS.Blending.SourceRGBFactor = SourceBlendingFactor.SourceAlpha;
            fillRS.Blending.SourceAlphaFactor = SourceBlendingFactor.SourceAlpha;
            fillRS.Blending.DestinationRGBFactor = DestinationBlendingFactor.OneMinusSourceAlpha;
            fillRS.Blending.DestinationAlphaFactor = DestinationBlendingFactor.OneMinusSourceAlpha;
            

            ShaderProgram fillSP = Device.CreateShaderProgram(
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.FillVS.glsl"),
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.FillFS.glsl"));
            _fillLogarithmicDepth = (Uniform<bool>)fillSP.Uniforms["u_logarithmicDepth"];
            _fillLogarithmicDepthConstant = (Uniform<float>)fillSP.Uniforms["u_logarithmicDepthConstant"];
            _fillColorUniform = (Uniform<Vector3F>)fillSP.Uniforms["u_color"];
            _fillAlphaUniform = (Uniform<float>)fillSP.Uniforms["u_alpha"];

            LogarithmicDepthConstant = 1;
            FillColor = Color.Gray;
            FillTranslucency = 0.5f;
            fillRS.DepthMask = false;
            ///////////////////////////////////////////////////////////////////

            _slices = slices ;
            _positionBuffer = Device.CreateVertexBuffer(BufferHint.StaticDraw, 2 * (_slices+1) * SizeInBytes<Vector3F>.Value);
            ushort[] indices = new ushort[_slices + 3 * (_slices)];
            for (ushort i = 0; i < _slices; i++) // base of cone will be line
                indices[i] = (ushort)(i + 1);
            for (ushort i = 0; i < _slices - 1; i++)
            {
                indices[_slices + i * 3] = 0;
                indices[_slices + i * 3 + 1] = (ushort)(i + 1);
                indices[_slices + i * 3 + 2] = (ushort)(i + 2);
            }
            // For last triangle connect first and last vertices
            indices[_slices + (_slices - 1) * 3] = 0;
            indices[_slices + (_slices - 1) * 3 + 1] = (ushort)(_slices);
            indices[_slices + (_slices - 1) * 3 + 2] = (ushort)(1);
            IndexBuffer indexBuffer = Device.CreateIndexBuffer(BufferHint.StaticDraw, indices.Length * sizeof(ushort));
            indexBuffer.CopyFromSystemMemory(indices);

            int stride = 2 * SizeInBytes<Vector3F>.Value;
            _va = context.CreateVertexArray();
            _va.Attributes[VertexLocations.PositionHigh] =
                new VertexBufferAttribute(_positionBuffer, ComponentDatatype.Float, 3, false, 0, stride);
            _va.Attributes[VertexLocations.PositionLow] =
                new VertexBufferAttribute(_positionBuffer, ComponentDatatype.Float, 3, false, SizeInBytes<Vector3F>.Value, stride);
            _va.IndexBuffer = indexBuffer;

            Show = true;
            ShowOutline = true;
            ShowFill = true;

            ///////////////////////////////////////////////////////////////////

            _drawStateLine = new DrawState(lineRS, lineSP, _va);
            _drawStateFill = new DrawState(fillRS, fillSP, _va);
            
            Origin = Vector3D.Zero;
            Target = Vector3D.Zero;
            _height = 0;
            _radius = 0;
        }

        public void SetCone(Vector3D origin, Vector3D target, double height, double radius)
        {
            _origin = origin;
            _target = target;
            _height = height;
            _radius = radius;
            _dirty = true;
        }

        private void Update()
        {
            if (_dirty)
            {
                Vector3F[] positions = new Vector3F[2*(_slices + 1)];
                
                EmulatedVector3D p0 = new EmulatedVector3D(_origin);
                positions[0] = p0.High;
                positions[1] = p0.Low;
                
                var vertices = RenderCone(_target, _origin, _height, _radius, _slices);
                for (int i = 1; i < _slices + 1; i++)
                {
                    EmulatedVector3D p = new EmulatedVector3D(vertices[i - 1]);
                    positions[i * 2] = p.High;
                    positions[i * 2 + 1] = p.Low;
                }

                _positionBuffer.CopyFromSystemMemory(positions);

                _dirty = false;
            }
        }

        public void Render(Context context, SceneState sceneState)
        {
            Verify.ThrowIfNull(context);
            Verify.ThrowIfNull(sceneState);

            if (Show)
            {
                Update();

                if (ShowOutline)
                {
                    //
                    // Pass 1:  Outline
                    //
                    _lineFillDistance.Value = (float)(OutlineWidth * 0.5 * sceneState.HighResolutionSnapScale);
                    context.Draw(OpenTK.Graphics.OpenGL.PrimitiveType.LineLoop, 0, _slices, _drawStateLine, sceneState);
                }

                if (ShowFill)
                {
                    //
                    // Pass 2:  Fill
                    //
                    context.Draw(OpenTK.Graphics.OpenGL.PrimitiveType.Triangles, _slices, (_slices)*3, _drawStateFill, sceneState);
                }
            }
        }

        public DepthTestFunction DepthTestFunction
        {
            get { return _drawStateLine.RenderState.DepthTest.Function; }
            set 
            {
                _drawStateLine.RenderState.DepthTest.Function = value;
                _drawStateFill.RenderState.DepthTest.Function = value; 
            }
        }

        public Vector3D Origin
        {
            get { return _origin; }

            set
            {
                if (_origin != value)
                {
                    _origin = value;
                    _dirty = true;
                }
            }
        }
        public Vector3D Target
        {
            get { return _target; }

            set
            {
                if (_target != value)
                {
                    _target = value;
                    _dirty = true;
                }
            }
        }

        public int Slices 
        {
            get { return _slices; }
        }
               
        public double OutlineWidth { get; set; }
        public bool Show { get; set; }
        public bool ShowOutline { get; set; }
        public bool ShowFill { get; set; }

        public bool LogarithmicDepth
        {
            get { return _lineLogarithmicDepth.Value; }
            set
            {
                _lineLogarithmicDepth.Value = value;
                _fillLogarithmicDepth.Value = value;
            }
        }

        public float LogarithmicDepthConstant
        {
            get { return _lineLogarithmicDepthConstant.Value; }
            set
            {
                _lineLogarithmicDepthConstant.Value = value;
                _fillLogarithmicDepthConstant.Value = value;
            }
        }

        public Color OutlineColor
        {
            get { return _lineColor; }

            set
            {
                _lineColor = value;
                _lineColorUniform.Value = new Vector3F(value.R / 255.0f, value.G / 255.0f, value.B / 255.0f);
            }
        }

        public Color FillColor
        {
            get { return _fillColor; }

            set
            {
                _fillColor = value;
                _fillColorUniform.Value = new Vector3F(value.R / 255.0f, value.G / 255.0f, value.B / 255.0f);
            }
        }

        public float FillTranslucency
        {
            get { return _fillTranslucency; }

            set
            {
                _fillTranslucency = value;
                _fillAlphaUniform.Value = 1.0f - value;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _drawStateLine.ShaderProgram.Dispose();
            _drawStateFill.ShaderProgram.Dispose();
            _positionBuffer.Dispose();
            _va.IndexBuffer.Dispose();
            _va.Dispose();
        }

        #endregion

        private readonly DrawState _drawStateLine;
        private readonly Uniform<bool> _lineLogarithmicDepth;
        private readonly Uniform<float> _lineLogarithmicDepthConstant;

        private readonly Uniform<float> _lineFillDistance;
        private readonly Uniform<Vector3F> _lineColorUniform;
        private Color _lineColor;

        private readonly DrawState _drawStateFill;
        private readonly Uniform<bool> _fillLogarithmicDepth;
        private readonly Uniform<float> _fillLogarithmicDepthConstant;

        private readonly Uniform<Vector3F> _fillColorUniform;
        private Color _fillColor;
        private readonly Uniform<float> _fillAlphaUniform;
        private float _fillTranslucency;

        private readonly VertexBuffer _positionBuffer;
        private readonly VertexArray _va;

        private bool _dirty;
        private Vector3D _origin;
        private Vector3D _target;
        private double _height, _radius;
        private int _slices;
    }
}