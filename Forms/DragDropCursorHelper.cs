// Forms/DragDropCursorHelper.cs
namespace MapperIce.Forms;

/// <summary>
/// Без AllowDrop=true контрол вообще не зарегистрирован как OLE drop-target,
/// и Windows рисует поверх курсора жёсткий системный значок "запрета" —
/// его не подавить никаким GiveFeedback. Регистрируем панель как валидный
/// таргет, но сразу отклоняем дроп (Effect = None); тогда ProtoList_GiveFeedback
/// увидит Effect != Copy и подставит обычную стрелку вместо "запрета".
/// </summary>
public static class DragDropCursorHelper
{
    public static void SuppressForbiddenCursor(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += (s, e) => e.Effect = DragDropEffects.None;
        control.DragOver += (s, e) => e.Effect = DragDropEffects.None;
    }
}