Public Class MyComboBoxItem
    Inherits ComboBoxItem

    '基础

    Public Uuid As Integer = GetUuid()
    Public Sub New()
        Style = FindResource("MyComboBoxItem")
    End Sub

    '指向动画

    Private Const AnimationTimeIn As Integer = 100
    Private Const AnimationTimeOut As Integer = 300
    Private BackColorName As String, FontOpacity As Double

    Private Sub RefreshColor() Handles Me.IsEnabledChanged
        '悬停 / 选中的底色改由控件模板的触发器负责：
        '  1) 原来用的 ColorBrush8 就是纯白，悬停等于没变化，只看到文字在动；
        '  2) 原来靠 MouseMove / MouseLeave + 300ms 动画切换，鼠标快速移开时容易漏掉、停留不消失。
        '这里只保留"被禁用时整体变淡"。
        Try
            AniStop("ComboBoxItem Color " & Uuid)
            Opacity = If(IsEnabled, 1.0, 0.4)
        Catch ex As Exception
        End Try
    End Sub
    Public Overrides Function Tostring() As String
        Return Content.ToString
    End Function
    Public Shared Widening Operator CType(Value As MyComboBoxItem) As String
        Return Value.Content.ToString
    End Operator


End Class
