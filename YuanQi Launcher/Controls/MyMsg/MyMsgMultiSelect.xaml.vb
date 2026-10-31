Public Class MyMsgMultiSelect

    Private ReadOnly MyConverter As MyMsgBoxConverter
    Private ReadOnly Uuid As Integer = GetUuid()

    Public Sub New(Converter As MyMsgBoxConverter)
        Try

            InitializeComponent()
            Btn1.Name = Btn1.Name & GetUuid()
            Btn2.Name = Btn2.Name & GetUuid()
            MyConverter = Converter
            LabTitle.Text = Converter.Title
            Btn1.Text = Converter.Button1
            Btn2.Text = Converter.Button2
            Btn2.Visibility = If(Converter.Button2 = "", Visibility.Collapsed, Visibility.Visible)
            ShapeLine.StrokeThickness = GetWPFSize(1)
            '添加勾选控件
            Btn1.IsEnabled = False
            For Each Content In Converter.Content
                Content = MyVirtualizingElement.TryInit(Content)
                Dim Item As MyListItem = Nothing
                If TypeOf Content Is MyListItem Then
                    Item = CType(Content, MyListItem)
                    Item.Type = MyListItem.CheckType.CheckBox
                Else
                    '兜底：不是列表项的控件也放进去，只是无法勾选
                    PanSelection.Children.Add(Content)
                    Continue For
                End If
                Item.MinHeight = 34
                PanSelection.Children.Add(Item)
                AddHandler Item.Check, AddressOf OnChecked
                AddHandler Item.Changed, AddressOf OnChanged
            Next
            RefreshBtn()

        Catch ex As Exception
            Logger.Error(ex, "多选弹窗初始化失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub Load(sender As Object, e As EventArgs) Handles MyBase.Loaded
        Try

            'UI 初始化
            If Btn2.IsVisible AndAlso Not Btn1.ColorType = MyButton.ColorState.Red Then Btn1.ColorType = MyButton.ColorState.Highlight
            '动画
            Opacity = 0
            AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(90, 0, 0, 0) - FrmMain.PanMsg.Background, 200), "PanMsg Background")
            AniStart({
                AaOpacity(Me, 1, 120, 60),
                AaDouble(Sub(i) TransformPos.Y += i, -TransformPos.Y, 300, 60, New AniEaseOutBack(AniEasePower.Weak)),
                AaDouble(Sub(i) TransformRotate.Angle += i, -TransformRotate.Angle, 300, 60, New AniEaseOutFluent(AniEasePower.Weak))
            }, "MyMsgBox " & Uuid)
            Logger.Info($"多选弹窗：{LabTitle.Text}")

        Catch ex As Exception
            Logger.Error(ex, "多选弹窗加载失败", LogBehavior.Toast)
        End Try
    End Sub
    Private Sub Close()
        '结束线程阻塞
        MyConverter.WaitFrame.Continue = False
        Interop.ComponentDispatcher.PopModal()
        '动画
        AniStart({
            AaCode(
            Sub()
                If Not WaitingMyMsgBox.Any() Then
                    AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(0, 0, 0, 0) - FrmMain.PanMsg.Background, 200, Ease:=New AniEaseOutFluent(AniEasePower.Weak)))
                End If
            End Sub, 30),
            AaOpacity(Me, -Opacity, 80, 20),
            AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 150, 0, New AniEaseOutFluent),
            AaDouble(Sub(i) TransformRotate.Angle += i, 6 - TransformRotate.Angle, 150, 0, New AniEaseInFluent(AniEasePower.Weak)),
            AaCode(Sub() CType(Parent, Grid).Children.Remove(Me), , True)
        }, "MyMsgBox " & Uuid)
    End Sub

    Public Sub Btn1_Click() Handles Btn1.Click
        If MyConverter.IsExited Then Return
        Dim Result As New List(Of Integer)
        For i = 0 To PanSelection.Children.Count - 1
            Dim Item = TryCast(PanSelection.Children(i), MyListItem)
            If Item IsNot Nothing AndAlso Item.Checked Then Result.Add(i)
        Next
        If Result.Count = 0 Then Return
        MyConverter.IsExited = True
        MyConverter.Result = Result
        Close()
    End Sub
    Public Sub Btn2_Click() Handles Btn2.Click
        If MyConverter.IsExited Then Return
        MyConverter.IsExited = True
        MyConverter.Result = Nothing
        Close()
    End Sub

    '全选 / 取消全选
    Private Sub BtnCheckAll_Click() Handles BtnCheckAll.Click
        Dim Target As Boolean = Not IsAllChecked()
        For Each Item In SelectableItems()
            Item.Checked = Target
        Next
        RefreshBtn()
    End Sub

    '反选
    Private Sub BtnInvert_Click() Handles BtnInvert.Click
        For Each Item In SelectableItems()
            Item.Checked = Not Item.Checked
        Next
        RefreshBtn()
    End Sub

    '清空
    Private Sub BtnClear_Click() Handles BtnClear.Click
        For Each Item In SelectableItems()
            Item.Checked = False
        Next
        RefreshBtn()
    End Sub

    Private Iterator Function SelectableItems() As IEnumerable(Of MyListItem)
        For Each Control In PanSelection.Children
            Dim Item = TryCast(Control, MyListItem)
            If Item IsNot Nothing Then Yield Item
        Next
    End Function
    Private Function IsAllChecked() As Boolean
        Dim AnyItem As Boolean = False
        For Each Item In SelectableItems()
            If Not Item.Checked Then Return False
            AnyItem = True
        Next
        Return AnyItem
    End Function

    Private Sub OnChecked(sender As Object, e As RouteEventArgs)
        RefreshBtn()
    End Sub
    '取消勾选时只会触发 Changed，需要一并刷新
    Private Sub OnChanged(sender As Object, e As RouteEventArgs)
        RefreshBtn()
    End Sub
    Private Sub RefreshBtn()
        Try
            Dim Count As Integer = 0
            Dim Total As Integer = 0
            For Each Item In SelectableItems()
                Total += 1
                If Item.Checked Then Count += 1
            Next
            Btn1.IsEnabled = Count > 0
            LabCount.Text = $"已选 {Count} / {Total} 项"
            '按钮文字变化不改变宽度，避免弹窗抖动
            BtnCheckAll.Text = If(Total > 0 AndAlso Count = Total, "取消全选", "全选")
            Btn1.Text = MyConverter.Button1
        Catch ex As Exception
            Logger.Warn(ex, "刷新多选按钮失败")
        End Try
    End Sub

    Private Sub Drag(sender As Object, e As MouseButtonEventArgs) Handles PanBorder.MouseLeftButtonDown, LabTitle.MouseLeftButtonDown
        On Error Resume Next
        If e.GetPosition(ShapeLine).Y <= 2 Then FrmMain.DragMove()
    End Sub

End Class
