using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Zeigt die Ahnentafel der Dynastie als grafischen Stammbaum (Issue #9): je Generation Oberhaupt,
/// Ehepartner und Kinder mit Geburts-/Todesjahren, verbunden durch die Dynastielinie. Wird als immer
/// verfügbares Privileg geöffnet. Rechtsklick schließt.
/// </summary>
public partial class AhnentafelDialog : DialogBase
{
	[Export]
	public NodePath BaumPath { get; set; }

	private AhnentafelBaum _baum;

	protected override void OnReady()
	{
		_baum = GetNode<AhnentafelBaum>(BaumPath);
	}

	/// <summary>Öffnet die Ahnentafel des aktiven Spielers.</summary>
	public Task ShowDialog()
	{
		_baum.Baue(new AhnentafelManager().GetGenerationen());
		return ShowAndAwait();
	}
}
