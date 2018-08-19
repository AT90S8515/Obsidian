using Fantome.Libraries.League.Helpers.Cryptography;
using Fantome.Libraries.League.Helpers.Structures;
using Fantome.Libraries.League.IO.SCB;
using Fantome.Libraries.League.IO.SCO;
using Fantome.Libraries.League.IO.SimpleSkin;
using Fantome.Libraries.League.IO.WAD;
using Imaging.DDSReader;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Obsidian.Preview
{
    public class PreviewHandler
    {
        public PreviewHandlerMode Mode;

        public MainWindow Window { get; }
        public Viewport3D Viewport { get; }
        public Model3DGroup ModelGroup { get; }
        public Label PreviewNameLabel { get; }
        public Label PreviewTypeLabel { get; }
        public ComboBox PreviewTextureComboBox { get; }
        public ComboBox PreviewMeshesComboBox { get; }
        public PerspectiveCamera PreviewCamera { get; }
        public StackPanel PreviewStackPanel { get; }
        public System.Windows.Controls.Image PreviewImage { get; }
        public Expander PreviewExpander { get; }
        public MouseHandler MouseHandler { get; private set; } = new MouseHandler();
        public Tuple<string, ulong> PreSelect = null;

        private Dictionary<GeometryModel3D, string> _materials = new Dictionary<GeometryModel3D, string>();
        private Dictionary<MeshesComboBoxHolder, bool> _models = new Dictionary<MeshesComboBoxHolder, bool>();

        public PreviewHandler(MainWindow window, Viewport3D viewport, Model3DGroup modelGroup, Label previewNameLabel,
            Label previewTypeLabel, ComboBox previewTextureComboBox, ComboBox previewMeshesComboBox,
            PerspectiveCamera previewCamera, StackPanel previewStackPanel, System.Windows.Controls.Image previewImage, Expander previewExpander)
        {
            this.Window = window;
            this.Viewport = viewport;
            this.ModelGroup = modelGroup;
            this.PreviewNameLabel = previewNameLabel;
            this.PreviewTypeLabel = previewTypeLabel;
            this.PreviewTextureComboBox = previewTextureComboBox;
            this.PreviewMeshesComboBox = previewMeshesComboBox;
            this.PreviewCamera = previewCamera;
            this.PreviewStackPanel = previewStackPanel;
            this.PreviewImage = previewImage;
            this.PreviewExpander = previewExpander;

            this.PreviewMeshesComboBox.SelectionChanged += meshesComboBoxSelecChange;
            this.PreviewTextureComboBox.SelectionChanged += textureComboBoxChanged;

            this.PreviewTextureComboBox.IsEnabled = false;
            this.PreviewMeshesComboBox.IsEnabled = false;

            this.MouseHandler.Attach(window);
            this.MouseHandler.Slaves.Add(this.Viewport);
            this.MouseHandler.Enabled = true;
            Clear();
        }

        public void Clear()
        {
            this.PreviewNameLabel.Content = "Name: ";
            this.PreviewTypeLabel.Content = "Type: ";

            foreach (KeyValuePair<GeometryModel3D, string> pair in this._materials)
            {
                this.ModelGroup.Children.Remove(pair.Key);
            }

            this._materials.Clear();
            this._models.Clear();
            this.PreviewMeshesComboBox.Items.Clear();

            List<string> textureComboBoxData = new List<string>();
            textureComboBoxData.Add("default");
            foreach (KeyValuePair<ulong, string> stringHash in MainWindow.StringDictionary)
            {
                if (stringHash.Value.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                {
                    textureComboBoxData.Add(stringHash.Value);
                }
            }

            this.PreviewTextureComboBox.ItemsSource = textureComboBoxData;
            this.PreviewTextureComboBox.SelectedIndex = 0;
            this.MouseHandler.Reset();
        }

        private void HandleSCB(SCBFile entry, string namePath)
        {
            this.PreviewMeshesComboBox.IsEnabled = false;
            this.PreviewTypeLabel.Content = "Type: Static Object Mesh Binary";
            this.PreviewNameLabel.Content = "Name: " + namePath.Split('/').Last();

            GeometryModel3D model = ApplyMesh(entry);
            if (this.PreSelect != null)
            {
                WADEntry preselectEntry = this.Window.Wad.Entries.ToList().Find(x => x.XXHash == this.PreSelect.Item2);
                if (preselectEntry != null)
                {
                    ApplyMaterial(model, preselectEntry, this.PreSelect.Item1);
                }
            }
            else
            {
                ApplyMaterial(model);
            }
        }

        private void HandleSCO(SCOFile entry, string namePath)
        {
            this.PreviewMeshesComboBox.IsEnabled = false;
            this.PreviewTypeLabel.Content = "Type: Static Object Mesh";
            this.PreviewNameLabel.Content = "Name: " + namePath.Split('/').Last();

            GeometryModel3D model = ApplyMesh(entry);
            if (this.PreSelect != null)
            {
                WADEntry preselectEntry = this.Window.Wad.Entries.ToList().Find(x => x.XXHash == this.PreSelect.Item2);
                if (preselectEntry != null)
                {
                    ApplyMaterial(model, preselectEntry, this.PreSelect.Item1);
                }
            }
            else
            {
                ApplyMaterial(model);
            }
        }

        private void HandleDDS(WADEntry entry, string namePath)
        {
            if (this.Mode == PreviewHandlerMode.MeshPreview)
            {
                this.Mode = PreviewHandlerMode.TexturePreview;
                this.Viewport.Visibility = Visibility.Collapsed;
                this.PreviewImage.Visibility = Visibility.Visible;
                this.PreviewTextureComboBox.IsEnabled = false;
                this.PreviewMeshesComboBox.IsEnabled = false;
            }

            //Set Content metadata
            this.PreviewTypeLabel.Content = "Type: DirectDraw Surface";
            this.PreviewNameLabel.Content = "Name: " + namePath.Split('/').Last();

            //Save the DDS file into a Bitmap stream
            MemoryStream stream = new MemoryStream();
            Bitmap image = DDS.LoadImage(entry.GetContent(true));
            image.Save(stream, ImageFormat.Png);

            //Create a BitmapImage from the Bitmap stream
            BitmapImage imageSource = new BitmapImage();
            imageSource.CacheOption = BitmapCacheOption.OnLoad;
            imageSource.BeginInit();
            imageSource.StreamSource = stream;
            imageSource.EndInit();
            imageSource.Freeze();

            this.PreviewImage.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.PreviewImage.Source = imageSource;
                this.PreviewImage.Width = image.Width;
                this.PreviewImage.Height = image.Height;
            }));
        }

        private void HandleSKN(WADEntry entry, string namePath)
        {
            PreviewSimpleSkinInfoHolder result = GenerateSimpleSkinInfoHolder(namePath);
            if (result == null)
            {
                return;
            }

            SKNFile skn = new SKNFile(new MemoryStream(result.SKNEntry.GetContent(true)));

            foreach (SKNSubmesh submesh in skn.Submeshes)
            {
                GeometryModel3D model = ApplyMesh(submesh);
                this.PreviewNameLabel.Content = "Name: " + namePath.Split('/').Last();
                this.PreviewTypeLabel.Content = "Type: Simple Skin Mesh";

                if (this.PreSelect != null)
                {
                    WADEntry preselectEntry = this.Window.Wad.Entries.ToList().Find(x => x.XXHash == this.PreSelect.Item2);
                    if (preselectEntry != null)
                    {
                        ApplyMaterial(model, preselectEntry, this.PreSelect.Item1);
                    }
                }
                else
                {
                    if (result.Textures.Count > 0)
                    {
                        ApplyMaterial(model, result.Textures.First().Value, result.Textures.First().Key);
                    }
                    else
                    {
                        ApplyMaterial(model);
                    }
                }
            }

            this.PreSelect = null;
        }

        public void Handle(WADEntry entry, string namePath)
        {
            if (namePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
            {
                HandleDDS(entry, namePath);
                return;
            }

            if (this.Mode == PreviewHandlerMode.TexturePreview)
            {
                this.Mode = PreviewHandlerMode.MeshPreview;
                this.PreviewImage.Visibility = Visibility.Collapsed;
                this.Viewport.Visibility = Visibility.Visible;
            }

            Clear();
            this.PreviewTextureComboBox.IsEnabled = true;
            this.PreviewMeshesComboBox.IsEnabled = true;
            if (!this.PreviewExpander.IsExpanded)
            {
                this.PreviewExpander.IsExpanded = true;
            }

            if (namePath.EndsWith(".scb", StringComparison.OrdinalIgnoreCase))
            {
                HandleSCB(new SCBFile(new MemoryStream(entry.GetContent(true))), namePath);
            }
            else if (namePath.EndsWith(".sco", StringComparison.OrdinalIgnoreCase))
            {
                HandleSCO(new SCOFile(new MemoryStream(entry.GetContent(true))), namePath);
            }
            else if (namePath.EndsWith(".skn", StringComparison.OrdinalIgnoreCase))
            {
                HandleSKN(entry, namePath);
            }
        }

        public GeometryModel3D ApplyMesh(SKNSubmesh submesh)
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            foreach (SKNVertex vertex in submesh.Vertices)
            {
                mesh.Positions.Add(new Point3D(vertex.Position.X, vertex.Position.Y, vertex.Position.Z));
                mesh.Normals.Add(new Vector3D(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z));
                mesh.TextureCoordinates.Add(new System.Windows.Point(vertex.UV.X, vertex.UV.Y));
            }

            foreach (ushort index in submesh.GetNormalizedIndices())
            {
                mesh.TriangleIndices.Add(index);
            }

            GeometryModel3D model = new GeometryModel3D()
            {
                Geometry = mesh
            };
            this.ModelGroup.Children.Add(model);

            MeshesComboBoxHolder boxHolder = GetMeshesComboBoxHolder(model);

            this._models.Add(boxHolder, true);
            this.PreviewMeshesComboBox.Items.Add(boxHolder.ComboBoxItem);

            boxHolder.TextBlock.Text = submesh.Name;

            return model;
        }

        public GeometryModel3D ApplyMesh(SCBFile scb)
        {
            Int32Collection indices = new Int32Collection();
            PointCollection uvs = new PointCollection();

            foreach (KeyValuePair<string, List<SCBFace>> material in scb.Materials)
            {
                foreach (SCBFace face in material.Value)
                {
                    indices.Add((int)face.Indices[0]);
                    indices.Add((int)face.Indices[1]);
                    indices.Add((int)face.Indices[2]);

                    uvs.Add(new System.Windows.Point(face.UVs[0].X, face.UVs[0].Y));
                    uvs.Add(new System.Windows.Point(face.UVs[1].X, face.UVs[1].Y));
                    uvs.Add(new System.Windows.Point(face.UVs[2].X, face.UVs[2].Y));
                }
            }

            MeshGeometry3D mesh = new MeshGeometry3D()
            {
                TriangleIndices = indices,
                TextureCoordinates = uvs
            };

            foreach (Vector3 x in scb.Vertices)
            {
                mesh.Positions.Add(new Point3D(x.X, x.Y, x.Z));
            }

            GeometryModel3D model = new GeometryModel3D()
            {
                Geometry = mesh
            };

            this.ModelGroup.Children.Add(model);
            return model;
        }

        public GeometryModel3D ApplyMesh(SCOFile sco)
        {
            Int32Collection indices = new Int32Collection();
            PointCollection uvs = new PointCollection();

            foreach (KeyValuePair<string, List<SCOFace>> material in sco.Materials)
            {
                foreach (SCOFace face in material.Value)
                {
                    indices.Add((int)face.Indices[0]);
                    indices.Add((int)face.Indices[1]);
                    indices.Add((int)face.Indices[2]);

                    uvs.Add(new System.Windows.Point(face.UVs[0].X, face.UVs[0].Y));
                    uvs.Add(new System.Windows.Point(face.UVs[1].X, face.UVs[1].Y));
                    uvs.Add(new System.Windows.Point(face.UVs[2].X, face.UVs[2].Y));
                }
            }

            MeshGeometry3D mesh = new MeshGeometry3D()
            {
                TriangleIndices = indices,
                TextureCoordinates = uvs
            };

            foreach (Vector3 x in sco.Vertices)
            {
                mesh.Positions.Add(new Point3D(x.X, x.Y, x.Z));
            }

            GeometryModel3D model = new GeometryModel3D()
            {
                Geometry = mesh
            };

            this.ModelGroup.Children.Add(model);
            return model;
        }

        public void ApplyMaterial(GeometryModel3D model, WADEntry entry, string path)
        {
            BitmapImage bitmapImage = new BitmapImage();
            ImageBrush materialBrush = new ImageBrush()
            {
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                ViewportUnits = BrushMappingMode.Absolute,
                AlignmentY = AlignmentY.Top,
                AlignmentX = AlignmentX.Left,
                TileMode = TileMode.Tile,
                Stretch = Stretch.Fill,
                ImageSource = bitmapImage
            };

            bitmapImage.BeginInit();
            bitmapImage.StreamSource = new MemoryStream(entry.GetContent(true));
            bitmapImage.EndInit();

            model.Material = new DiffuseMaterial(materialBrush);
            if (!this._materials.ContainsKey(model))
            {
                this._materials.Add(model, path);
            }
            else
            {
                this._materials[model] = path;
            }

            this.PreviewTextureComboBox.SelectedItem = path;
        }

        public void ApplyMaterial(GeometryModel3D model)
        {
            model.Material = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb(69, 62, 61)));

            if (!this._materials.ContainsKey(model))
            {
                this._materials.Add(model, "default");
            }
            else
            {
                this._materials[model] = "default";
            }

            this.PreviewTextureComboBox.SelectedItem = "default";
        }

        private MeshesComboBoxHolder GetMeshesComboBoxHolder(GeometryModel3D model)
        {
            StackPanel stackPanel = new StackPanel()
            {
                Orientation = Orientation.Horizontal
            };
            ComboBoxItem comboBoxItem = new ComboBoxItem()
            {
                Content = stackPanel
            };
            CheckBox checkBox = new CheckBox()
            {
                Width = 20
            };
            TextBlock textBlock = new TextBlock()
            {
                Width = 250
            };

            stackPanel.Children.Add(checkBox);
            stackPanel.Children.Add(textBlock);

            return new MeshesComboBoxHolder(checkBox, textBlock, comboBoxItem, model, this.ModelGroup);
        }

        private WADEntry GetWADEntryByPath(string name)
        {
            using (XXHash64 xxHash = XXHash64.Create())
            {
                ulong hash = BitConverter.ToUInt64(xxHash.ComputeHash(Encoding.ASCII.GetBytes(name.ToLower())), 0);

                return this.Window.Wad.Entries.ToList().Find(x => x.XXHash == hash);
            }
        }

        public void Emit(WADEntry entry)
        {
            if (MainWindow.StringDictionary.ContainsKey(entry.XXHash))
            {
                string path = MainWindow.StringDictionary[entry.XXHash];
                if (CanHandle(path))
                {
                    Handle(entry, path);
                }
                else if (this.PreviewExpander.IsExpanded)
                {
                    this.PreviewExpander.IsExpanded = false;
                }
            }
        }

        private bool CanHandle(string path)
        {
            if (path.EndsWith(".skn", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".skl", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".scb", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".sco", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private PreviewSimpleSkinInfoHolder GenerateSimpleSkinInfoHolder(string namePath)
        {
            string entryName = Path.GetFileName(namePath);
            using (XXHash64 xxHash = XXHash64.Create())
            {
                if (namePath.EndsWith(".skn", StringComparison.OrdinalIgnoreCase))
                {
                    string sklPath = namePath.Replace(".skn", ".skl");
                    string texturePath = "";
                    if (!MainWindow.StringDictionary.ContainsKey(BitConverter.ToUInt64(xxHash.ComputeHash(Encoding.ASCII.GetBytes(sklPath.ToLower())), 0)))
                    {
                        return null;
                    }

                    bool foundTexture = false;
                    foreach (KeyValuePair<ulong, string> stringEntry in MainWindow.StringDictionary)
                    {
                        string pathLower = stringEntry.Value.ToLower();
                        if (pathLower.Contains("tx_cm"))
                        {
                            if (pathLower.Contains((entryName.ToLower())) || pathLower.Contains(entryName.ToLower() + "_body"))
                            {
                                if (pathLower.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (namePath.ToLower().Contains("/base/"))
                                    {
                                        if (pathLower.Contains("_base") || pathLower.Contains("body"))
                                        {
                                            foundTexture = true;
                                            texturePath = stringEntry.Value;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        foundTexture = true;
                                        texturePath = stringEntry.Value;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    var texturesMap = new Dictionary<string, WADEntry>();
                    if (foundTexture) texturesMap.Add(texturePath, GetWADEntryByPath(texturePath));

                    return new PreviewSimpleSkinInfoHolder(GetWADEntryByPath(namePath), GetWADEntryByPath(sklPath), texturesMap);
                }

                if (namePath.EndsWith(".skl", StringComparison.OrdinalIgnoreCase))
                {
                    string sknPath = namePath.Replace(".skl", ".skn");
                    string texturePath = "";
                    if (!MainWindow.StringDictionary.ContainsKey(BitConverter.ToUInt64(xxHash.ComputeHash(Encoding.ASCII.GetBytes(sknPath.ToLower())), 0)))
                    {
                        return null;
                    }

                    bool foundTexture = false;
                    foreach (KeyValuePair<ulong, string> stringEntry in MainWindow.StringDictionary)
                    {
                        string pathLower = stringEntry.Value.ToLower();
                        if (pathLower.Contains("tx_cm"))
                        {
                            if (pathLower.Contains((entryName.ToLower() + "_tx_cm")) || pathLower.Contains(entryName.ToLower() + "_body_tx_cm"))
                            {
                                if (pathLower.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (namePath.ToLower().Contains("/base/"))
                                    {
                                        if (pathLower.Contains("_base") || pathLower.Contains("body"))
                                        {
                                            foundTexture = true;
                                            texturePath = stringEntry.Value;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        foundTexture = true;
                                        texturePath = stringEntry.Value;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    Dictionary<string, WADEntry> texturesMap = new Dictionary<string, WADEntry>();
                    if (foundTexture)
                    {
                        texturesMap.Add(texturePath, GetWADEntryByPath(texturePath));
                    }

                    return new PreviewSimpleSkinInfoHolder(GetWADEntryByPath(sknPath), GetWADEntryByPath(namePath), texturesMap);
                }
            }

            return null;
        }

        private void meshesComboBoxSelecChange(object sender, SelectionChangedEventArgs e)
        {
            ((ComboBox)sender).SelectedItem = null;
        }

        private void textureComboBoxChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedValue = (sender as ComboBox).SelectedItem as string;

            if (selectedValue == "default")
            {
                List<GeometryModel3D> list = new List<GeometryModel3D>();
                foreach (KeyValuePair<GeometryModel3D, string> models in this._materials)
                {
                    list.Add(models.Key);
                    ApplyMaterial(models.Key);
                }
            }

            using (XXHash64 xxHash = XXHash64.Create())
            {
                ulong selectedValueHash = BitConverter.ToUInt64(xxHash.ComputeHash(Encoding.ASCII.GetBytes(selectedValue.ToLower())), 0);
                if (MainWindow.StringDictionary.ContainsKey(selectedValueHash))
                {
                    WADEntry entry = this.Window.Wad.Entries.ToList().Find(x => x.XXHash == selectedValueHash);
                    if (entry == null)
                    {
                        return;
                    }

                    List<GeometryModel3D> list = new List<GeometryModel3D>();
                    foreach (KeyValuePair<GeometryModel3D, string> models in this._materials)
                    {
                        list.Add(models.Key);
                        ApplyMaterial(models.Key, entry, selectedValue);
                    }
                }
            }
        }
    }

    public enum PreviewHandlerMode
    {
        MeshPreview,
        TexturePreview
    }
}