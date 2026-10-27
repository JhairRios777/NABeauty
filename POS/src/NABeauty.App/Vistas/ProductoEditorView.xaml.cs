using System.Windows.Controls;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.App.Vistas;

public partial class ProductoEditorView : UserControl
{
    public ProductoEditorView()
    {
        InitializeComponent();
        // Producto nuevo: el cursor empieza en el nombre
        Loaded += (_, _) => { if (DataContext is ProductoEditorViewModel { EsNuevo: true }) CampoNombre.Focus(); };
    }
}
