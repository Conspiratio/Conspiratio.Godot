using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Geburt eines Kindes (Migration von Ereignis_Geburt): verkündet Sohn oder Tochter und lässt den
/// Spieler den Namen eingeben, unter dem das Kind angelegt wird.
/// </summary>
public partial class GeburtDialog : Control
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	[Export]
	public NodePath LineEditNamePath { get; set; }

	private Label _labelText;
	private LineEdit _lineEditName;

	private FamilieManager _familieManager;
	private bool _maennlich;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelText = GetNode<Label>(LabelTextPath);
		_lineEditName = GetNode<LineEdit>(LineEditNamePath);

		HideAndDisableInput();
	}

	public async Task ShowDialog(FamilieManager familieManager)
	{
		_familieManager = familieManager;
		_maennlich = _familieManager.ErmittleGeburtGeschlecht();

		bool spielerMaennlich = SW_IstSpielerMaennlich();
		string text = spielerMaennlich ? "Eure Frau hat Euch " : "Ihr habt ";
		text += _maennlich ? "einen Sohn " : "eine Tochter ";
		text += spielerMaennlich ? "geschenkt!" : "geboren!";
		text += "\nWelchen Namen soll das Kind tragen?";

		_labelText.Text = text;
		_lineEditName.Clear();
		_lineEditName.GrabFocus();

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
	}

	private static bool SW_IstSpielerMaennlich()
	{
		return Conspiratio.Lib.Gameplay.Spielwelt.SW.Dynamisch.GetAktHum().GetMaennlich();
	}

	private void _on_line_edit_name_text_submitted(string newText)
	{
		string name = string.IsNullOrWhiteSpace(_lineEditName.Text) ? "Namenlos" : _lineEditName.Text;

		_familieManager.FuehreGeburtDurch(_maennlich, name);

		HideAndDisableInput();
		_dialogClosed?.TrySetResult(true);
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	private Task CloseDialogTask()
	{
		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}
}
