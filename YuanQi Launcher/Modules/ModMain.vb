Imports System.Collections.ObjectModel
Imports System.Windows.Interop
Imports System.Windows.Threading

Public Module ModMain

#Region "弹出提示"

    ''' <summary>
    ''' 提示信息的种类。
    ''' 该枚举在自定义事件中使用，是公开 API 的一部分。
    ''' </summary>
    Public Enum HintType
        Blue
        Green
        Red
    End Enum
    Private Structure HintMessage
        Public Text As String
        Public Type As HintType
        Public Log As Boolean
    End Structure

    ''' <summary>
    ''' 等待弹出的提示列表。以 {String, HintType, Log As Boolean} 形式存储为数组。
    ''' </summary>
    Private HintWaiting As New ConcurrentQueue(Of HintMessage)

    ''' <summary>全局悬浮提示延迟（毫秒）：鼠标停在按钮上要等这么久才弹出跟随鼠标的提示。
    ''' 通过在 Application 静态构造里覆写 ToolTipService 元数据默认值实现（见 Application.xaml.vb）。</summary>
    Public Const ToolTipShowDelay As Integer = 1000

    ''' <summary>
    ''' 在窗口左下角弹出提示文本。
    ''' </summary>
    Public Sub Hint(Text As String, Optional Type As HintType = HintType.Blue, Optional Log As Boolean = True)
        HintWaiting.Enqueue(New HintMessage With {.Text = If(Text, ""), .Type = Type, .Log = Log})
    End Sub

    Private Sub HintTick()
        Try
            Dim CurrentHint As HintMessage = Nothing
            Do While HintWaiting.TryDequeue(CurrentHint)
                CurrentHint.Text = CurrentHint.Text.ReplaceLineEndings(" ", mergeMultiple:=True)
                '超量提示直接忽略
                If FrmMain.PanHint.Children.Count >= 4 Then GoTo EndHint
                '检查是否有重复提示
                Dim DoubleStack As Border = Nothing
                For Each stack As Border In FrmMain.PanHint.Children
                    If stack.Tag IsNot Nothing AndAlso stack.Tag.Length >= 3 AndAlso CBool(stack.Tag(0)) AndAlso
                       String.Equals(CStr(stack.Tag(2)), CurrentHint.Text) Then DoubleStack = stack
                Next
                '强调色
                Dim AccentColor As MyColor
                Select Case CurrentHint.Type
                    Case HintType.Red
                        AccentColor = New MyColor(196, 43, 28)
                    Case HintType.Green
                        AccentColor = New MyColor(61, 138, 45)
                    Case Else
                        '普通（蓝色）提示跟随主题强调色，原先固定为主题绿
                        Try
                            AccentColor = New MyColor(Application.Current.FindResource("ColorBrush3"))
                        Catch
                            AccentColor = New MyColor(61, 138, 45)
                        End Try
                End Select
                If DoubleStack IsNot Nothing Then
                    '有重复提示：重播一次进入动画，并重新计时
                    AniStop("Hint Hide " & DoubleStack.Tag(1))
                    DoubleStack.RenderTransform = New TranslateTransform(0, 0)
                    DoubleStack.Opacity = 1
                    '只用透明度脉冲表示"重复提示"，不再左右位移，避免多条提示的左边缘看起来不对齐
                    AniStart({
                        AaOpacity(DoubleStack, -0.35, 100, , New AniEaseOutFluent),
                        AaOpacity(DoubleStack, 0.35, 180, 100, New AniEaseOutFluent)
                    }, "Hint Show " & DoubleStack.Tag(1))
                    ScheduleHintHide(DoubleStack, CurrentHint.Text)
                Else
                    '新建气泡
                    Dim NewHintControl As New Border With {
                        .Tag = {True, GetUuid(), CurrentHint.Text},
                        .Margin = New Thickness(0, 0, 0, 8),
                        .Opacity = 0,
                        .HorizontalAlignment = HorizontalAlignment.Left,
                        .CornerRadius = New CornerRadius(10),
                        .BorderThickness = New Thickness(1),
                        .MaxWidth = 440,
                        .RenderTransform = New TranslateTransform(0, 0)
                    }
                    NewHintControl.Background = New MyColor(255, 255, 255)
                    NewHintControl.SetResourceReference(Border.BorderBrushProperty, "BrushBorder")
                    Dim Shadow As New Effects.DropShadowEffect With {.BlurRadius = 14, .ShadowDepth = 2, .Direction = 270, .Opacity = 0.16, .Color = New MyColor(60, 30, 45)}
                    NewHintControl.Effect = Shadow

                    Dim Layout As New Grid With {.Margin = New Thickness(12, 9, 16, 9)}
                    Layout.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(3)})
                    Layout.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
                    Layout.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
                    Dim Bar As New Border With {.Width = 3, .CornerRadius = New CornerRadius(2), .Background = AccentColor, .Margin = New Thickness(0, 1, 10, 1)}
                    Layout.Children.Add(Bar)
                    Dim Icon As New TextBlock With {
                        .Text = If(CurrentHint.Type = HintType.Red, "!", "✓"),
                        .FontSize = 12, .FontWeight = FontWeights.Bold, .Foreground = AccentColor,
                        .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 8, 0)
                    }
                    Grid.SetColumn(Icon, 1)
                    Layout.Children.Add(Icon)
                    Dim Label As New TextBlock With {
                        .Text = CurrentHint.Text, .FontSize = 12.5, .Foreground = New MyColor(38, 35, 38),
                        .TextTrimming = TextTrimming.CharacterEllipsis, .VerticalAlignment = VerticalAlignment.Center,
                        .MaxWidth = 380
                    }
                    Grid.SetColumn(Label, 2)
                    Layout.Children.Add(Label)
                    NewHintControl.Child = Layout

                    FrmMain.PanHint.Children.Add(NewHintControl)
                    '淡入即可；不做横向位移，保证每条提示的左边缘一致
                    AniStart(AaOpacity(NewHintControl, 1, 140), "Hint Show " & NewHintControl.Tag(1))
                    ScheduleHintHide(NewHintControl, CurrentHint.Text)
                End If
EndHint:
                If CurrentHint.Log Then Logger.Info($"弹出提示：{CurrentHint.Text}")
            Loop
        Catch ex As Exception
            Logger.Info(ex, "显示弹出提示失败")
        End Try
    End Sub

    ''' <summary>
    ''' 安排一个提示气泡的自动消失。
    ''' </summary>
    Private Sub ScheduleHintHide(Control As Border, Text As String)
        Dim Delay As Double = (2000 + Text.Length.Clamp(5, 30) * 110) * AniSpeed
        AniStart({
            AaOpacity(Control, -1, 180, Delay),
            AaX(Control, -10, 180, Delay),
            AaCode(Sub() Control.Tag(0) = False, Delay),
            AaCode(Sub() FrmMain.PanHint.Children.Remove(Control), Delay + 220, True)
        }, "Hint Hide " & Control.Tag(1))
    End Sub

    Private Sub HideAllHint()
        For Each Control As Border In FrmMain.PanHint.Children.Cast(Of Border)().ToArray()
            Control.IsHitTestVisible = False
            AniStart({
                AaX(Control, -10, 180, , New AniEaseInFluent),
                AaOpacity(Control, -1, 150, , New AniEaseInFluent),
                AaCode(Sub() Control.Tag(0) = False),
                AaCode(Sub() FrmMain.PanHint.Children.Remove(Control), , True)
            }, "Hint Hide " & Control.Tag(1))
        Next
    End Sub

#End Region

#Region "弹窗"

    ''' <summary>
    ''' 存储弹窗信息的转换器。
    ''' </summary>
    Public Class MyMsgBoxConverter
        Public Type As MyMsgBoxType
        Public Title As String
        Public Text As String
        ''' <summary>添加账户弹窗：要添加的账户类型。</summary>
        Public AccountType As McLoginType
        ''' <summary>添加账户弹窗：预填的认证服务器地址（如 LittleSkin）。</summary>
        Public DefaultServer As String = ""
        ''' <summary>
        ''' 输入模式：文本框的文本。
        ''' 选择模式：需要放进去的 IEnumberable(Of IMyRadio)。
        ''' 登录模式：登录步骤 1 中返回的 JSON。
        ''' </summary>
        Public Content As Object
        ''' <summary>
        ''' 输入模式：输入验证规则。
        ''' </summary>
        Public ValidateRules As Collection(Of Validate)
        ''' <summary>
        ''' 输入模式：提示文本。
        ''' </summary>
        Public HintText As String = ""
        ''' <summary>
        ''' 有多个按钮时，是否给第一个按钮加高亮。
        ''' </summary>
        Public HighLight As Boolean
        Public Button1 As String = "确定"
        Public Button2 As String = ""
        Public Button3 As String = ""
        ''' <summary>
        ''' 点击第一个按钮将执行该方法，不关闭弹窗。
        ''' </summary>
        Public Button1Action As Action = Nothing
        ''' <summary>
        ''' 点击第二个按钮将执行该方法，不关闭弹窗。
        ''' </summary>
        Public Button2Action As Action = Nothing
        ''' <summary>
        ''' 点击第三个按钮将执行该方法，不关闭弹窗。
        ''' </summary>
        Public Button3Action As Action = Nothing
        Public IsWarn As Boolean = False
        Public ForceWait As Boolean = False
        Public WaitFrame As New DispatcherFrame(True)
        ''' <summary>
        ''' 弹窗是否已经关闭。
        ''' </summary>
        Public IsExited As Boolean = False
        ''' <summary>
        ''' 输入模式：输入的文本。若点击了 非 第一个按钮，则为 Nothing。
        ''' 选择模式：点击的按钮编号，从 1 开始。
        ''' 登录模式：字符串数组 {AccessToken, RefreshToken} 或一个 Exception。
        ''' </summary>
        Public Result As Object
    End Class
    Public Enum MyMsgBoxType
        Text
        [Select]
        MultiSelect
        Input
        Login
        AccountAdd
        Guide
    End Enum

    ''' <summary>
    ''' 显示弹窗，返回点击按钮的编号（从 1 开始）。
    ''' </summary>
    ''' <param name="Title">弹窗的标题。</param>
    ''' <param name="Caption">弹窗的内容。</param>
    ''' <param name="Button1">显示的第一个按钮，默认为“确定”。</param>
    ''' <param name="Button2">显示的第二个按钮，默认为空。</param>
    ''' <param name="Button3">显示的第三个按钮，默认为空。</param>
    ''' <param name="Button1Action">点击第一个按钮将执行该方法，不关闭弹窗。</param>
    ''' <param name="Button2Action">点击第二个按钮将执行该方法，不关闭弹窗。</param>
    ''' <param name="Button3Action">点击第三个按钮将执行该方法，不关闭弹窗。</param>
    ''' <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    Public Function MyMsgBox(Caption As String, Optional Title As String = "提示",
                             Optional Button1 As String = "确定", Optional Button2 As String = "", Optional Button3 As String = "",
                             Optional IsWarn As Boolean = False, Optional HighLight As Boolean = True, Optional ForceWait As Boolean = False,
                             Optional Button1Action As Action = Nothing, Optional Button2Action As Action = Nothing, Optional Button3Action As Action = Nothing) As Integer
        '将弹窗列入队列
        Dim Converter As New MyMsgBoxConverter With {.Type = MyMsgBoxType.Text, .Button1 = Button1, .Button2 = Button2, .Button3 = Button3, .Text = Caption, .IsWarn = IsWarn, .Title = Title, .HighLight = HighLight, .ForceWait = True, .Button1Action = Button1Action, .Button2Action = Button2Action, .Button3Action = Button3Action}
        If Button2.Length > 0 OrElse ForceWait Then
            '若有多个按钮则开始等待
            If FrmMain Is Nothing OrElse FrmMain.PanMsg Is Nothing AndAlso RunInUi() Then
                '主窗体尚未加载，用老土的弹窗来替代
                If Button2.Length > 0 Then
                    Dim RawResult As MsgBoxResult =
                        MsgBox(Caption, If(Button3.Length > 0, MsgBoxStyle.YesNoCancel, MsgBoxStyle.YesNo) + If(IsWarn, MsgBoxStyle.Critical, MsgBoxStyle.Question), Title)
                    Select Case RawResult
                        Case MsgBoxResult.Yes
                            Converter.Result = 1
                        Case MsgBoxResult.No
                            Converter.Result = 2
                        Case MsgBoxResult.Cancel
                            Converter.Result = 3
                    End Select
                Else
                    MsgBox(Caption, MsgBoxStyle.OkOnly + If(IsWarn, MsgBoxStyle.Critical, MsgBoxStyle.Question), Title)
                    Converter.Result = 1
                End If
                Logger.Warn($"主窗体加载完成前出现意料外的等待弹窗：{Button1},{Button2},{Button3}")
            Else
                WaitingMyMsgBox.Enqueue(Converter)
                Try
                    FrmMain.DragStop()
                    If RunInUi() Then MyMsgBoxTick()
                    ComponentDispatcher.PushModal()
                    Dispatcher.PushFrame(Converter.WaitFrame)
                Finally
                    ComponentDispatcher.PopModal()
                End Try
            End If
            Logger.Info($"普通弹框返回：{If(Converter.Result, "null")}")
            Return Converter.Result
        Else
            '不进行等待，直接返回
            WaitingMyMsgBox.Enqueue(Converter)
            Return 1
        End If
    End Function
    ''' <summary>
    ''' 显示输入框并返回输入的文本。若点击第二个按钮，则返回 Nothing。
    ''' </summary>
    ''' <param name="Title">弹窗的标题。</param>
    ''' <param name="ValidateRules">文本框的输入检测。</param>
    ''' <param name="Text">弹窗的介绍文本。</param>
    ''' <param name="DefaultInput">文本框的默认内容。</param>
    ''' <param name="HintText">文本框的提示内容。</param>
    ''' <param name="Button1">显示的第一个按钮，默认为“确定”。</param>
    ''' <param name="Button2">显示的第二个按钮，默认为“取消”。</param>
    ''' <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    Public Function MyMsgBoxInput(Title As String, Optional Text As String = Nothing, Optional DefaultInput As String = Nothing, Optional ValidateRules As Collection(Of Validate) = Nothing, Optional HintText As String = "", Optional Button1 As String = "确定", Optional Button2 As String = "取消", Optional IsWarn As Boolean = False) As String
        '将弹窗列入队列
        Dim Converter As New MyMsgBoxConverter With {.Text = If(Text, ""), .HintText = If(HintText, ""), .Type = MyMsgBoxType.Input, .ValidateRules = If(ValidateRules, New Collection(Of Validate)), .Button1 = Button1, .Button2 = Button2, .Content = DefaultInput, .IsWarn = IsWarn, .Title = Title}
        WaitingMyMsgBox.Enqueue(Converter)
        '虽然我也不知道这是啥但是能用就成了 :)
        Try
            If FrmMain IsNot Nothing Then FrmMain.DragStop()
            If RunInUi() Then MyMsgBoxTick()
            ComponentDispatcher.PushModal()
            Dispatcher.PushFrame(Converter.WaitFrame)
        Finally
            ComponentDispatcher.PopModal()
        End Try
        Logger.Info($"输入弹框返回：{If(Converter.Result, "null")}")
        Return Converter.Result
    End Function
    ''' <summary>
    ''' 显示选择框并返回选择的第几项（从 0 开始）。若点击第二个按钮，则返回 Nothing。
    ''' </summary>
    ''' <param name="Title">弹窗的标题。</param>
    ''' <param name="Button1">显示的第一个按钮，默认为 “确定”。</param>
    ''' <param name="Button2">显示的第二个按钮，默认为空。</param>
    ''' <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    Public Function MyMsgBoxSelect(Selections As IEnumerable(Of IMyRadio), Optional Title As String = "提示", Optional Button1 As String = "确定", Optional Button2 As String = "", Optional IsWarn As Boolean = False) As Integer?
        '将弹窗列入队列
        Dim Converter As New MyMsgBoxConverter With {.Type = MyMsgBoxType.Select, .Button1 = Button1, .Button2 = Button2, .Content = Selections, .IsWarn = IsWarn, .Title = Title}
        WaitingMyMsgBox.Enqueue(Converter)
        '虽然我也不知道这是啥但是能用就成了 :)
        Try
            If FrmMain IsNot Nothing Then FrmMain.DragStop()
            If RunInUi() Then MyMsgBoxTick()
            ComponentDispatcher.PushModal()
            Dispatcher.PushFrame(Converter.WaitFrame)
        Finally
            ComponentDispatcher.PopModal()
        End Try
        Logger.Info($"选择弹框返回：{If(Converter.Result, "null")}")
        Return Converter.Result
    End Function

    ''' <summary>
    ''' 显示多选框并返回所有被勾选的项的下标（从 0 开始）。若点击第二步按钮，则返回 Nothing。
    ''' </summary>
    ''' <param name="Title">弹窗的标题。</param>
    ''' <param name="Items">需要放进去的可勾选项（MyListItem 或 MyCheckBox）。</param>
    ''' <param name="Button1">显示的第一个按钮，默认为 “确定”。</param>
    ''' <param name="Button2">显示的第二个按钮，默认为 “取消”。</param>
    Public Function MyMsgBoxSelectMultiple(Items As IEnumerable(Of UIElement), Optional Title As String = "提示", Optional Button1 As String = "确定", Optional Button2 As String = "取消") As List(Of Integer)
        Dim Converter As New MyMsgBoxConverter With {.Type = MyMsgBoxType.MultiSelect, .Button1 = Button1, .Button2 = Button2, .Content = Items, .Title = Title}
        WaitingMyMsgBox.Enqueue(Converter)
        Try
            If FrmMain IsNot Nothing Then FrmMain.DragStop()
            If RunInUi() Then MyMsgBoxTick()
            ComponentDispatcher.PushModal()
            Dispatcher.PushFrame(Converter.WaitFrame)
        Finally
            ComponentDispatcher.PopModal()
        End Try
        Logger.Info($"多选弹框返回：{If(Converter.Result, "null")}")
        Return Converter.Result
    End Function

    ''' <summary>
    ''' 显示首次使用引导。返回 True 表示完整看完，False 表示跳过。
    ''' </summary>
    Public Function MyMsgBoxGuide() As Boolean
        Dim Converter As New MyMsgBoxConverter With {.Type = MyMsgBoxType.Guide, .Title = "使用引导", .Button1 = "下一步", .ForceWait = True}
        WaitingMyMsgBox.Enqueue(Converter)
        Try
            If FrmMain IsNot Nothing Then FrmMain.DragStop()
            If RunInUi() Then MyMsgBoxTick()
            ComponentDispatcher.PushModal()
            Dispatcher.PushFrame(Converter.WaitFrame)
        Finally
            ComponentDispatcher.PopModal()
        End Try
        Logger.Info($"使用引导弹窗返回：{If(Converter.Result, "null")}")
        Return If(Converter.Result, 2) = 1
    End Function

    ''' <summary>
    ''' 显示「添加账户」弹窗，返回新建的账户引用；取消则返回 Nothing。
    ''' </summary>
    Public Function MyMsgBoxAccountAdd(Type As McLoginType, Title As String, Optional DefaultServer As String = "") As AccountRef
        Dim Converter As New MyMsgBoxConverter With {.Type = MyMsgBoxType.AccountAdd, .AccountType = Type, .Button1 = "添加", .Button2 = "取消", .Title = Title, .DefaultServer = DefaultServer}
        WaitingMyMsgBox.Enqueue(Converter)
        Try
            If FrmMain IsNot Nothing Then FrmMain.DragStop()
            If RunInUi() Then MyMsgBoxTick()
            ComponentDispatcher.PushModal()
            Dispatcher.PushFrame(Converter.WaitFrame)
        Finally
            ComponentDispatcher.PopModal()
        End Try
        Return If(TypeOf Converter.Result Is AccountRef, CType(Converter.Result, AccountRef), Nothing)
    End Function

    ''' <summary>
    ''' 等待显示的弹窗。
    ''' </summary>
    Public WaitingMyMsgBox As New ConcurrentQueue(Of MyMsgBoxConverter)
    Public Sub MyMsgBoxTick()
        Try
            If FrmMain?.PanMsg Is Nothing OrElse FrmMain.WindowState = WindowState.Minimized Then Return
            Dim TargetMsgBox As MyMsgBoxConverter = Nothing
            If FrmMain.PanMsg.Children.Count > 0 Then
                '弹窗中
                FrmMain.PanMsg.Visibility = Visibility.Visible
            ElseIf WaitingMyMsgBox.TryDequeue(TargetMsgBox) Then
                '没有弹窗，显示一个等待的弹窗
                FrmMain.PanMsg.Visibility = Visibility.Visible
                Select Case TargetMsgBox.Type
                    Case MyMsgBoxType.Input
                        FrmMain.PanMsg.Children.Add(New MyMsgInput(TargetMsgBox))
                    Case MyMsgBoxType.Select
                        FrmMain.PanMsg.Children.Add(New MyMsgSelect(TargetMsgBox))
                    Case MyMsgBoxType.MultiSelect
                        FrmMain.PanMsg.Children.Add(New MyMsgMultiSelect(TargetMsgBox))
                    Case MyMsgBoxType.Text
                        FrmMain.PanMsg.Children.Add(New MyMsgText(TargetMsgBox))
                    Case MyMsgBoxType.Login
                        FrmMain.PanMsg.Children.Add(New MyMsgLogin(TargetMsgBox))
                    Case MyMsgBoxType.AccountAdd
                        FrmMain.PanMsg.Children.Add(New MyMsgAccountAdd(TargetMsgBox))
                    Case MyMsgBoxType.Guide
                        FrmMain.PanMsg.Children.Add(New MyMsgGuide(TargetMsgBox))
                End Select
            Else
                '没有弹窗，没有等待的弹窗
                If FrmMain.PanMsg.Visibility <> Visibility.Collapsed Then FrmMain.PanMsg.Visibility = Visibility.Collapsed
            End If
        Catch ex As Exception
            Logger.Error(ex, "处理等待中的弹窗失败")
        End Try
    End Sub

#End Region

#Region "页面声明"
    '在最后进行页面声明，避免颜色尚未加载完毕

    '窗体声明
    Public FrmMain As FormMain
    Public FrmStart As SplashScreen

    '页面声明（出于单元测试考虑，初始化页面已转入 FormMain 中）
    Public FrmLaunchLeft As PageLaunchLeft
    Public FrmLaunchRight As PageLaunchRight
    Public FrmSelectLeft As PageSelectLeft
    Public FrmSelectRight As PageSelectRight
    Public FrmSpeedLeft As PageSpeedLeft
    Public FrmSpeedRight As PageSpeedRight

    '联机页面声明
    Public FrmLinkMain As PageLinkMain

    '种子地图页面声明
    Public FrmSeedMapMain As PageSeedMap

    '下载页面声明
    Public FrmDownloadLeft As PageDownloadLeft
    Public FrmDownloadInstall As PageDownloadInstall
    Public FrmDownloadMod As PageDownloadMod
    Public FrmDownloadPack As PageDownloadPack
    Public FrmDownloadDataPack As PageDownloadDataPack
    Public FrmDownloadShader As PageDownloadShader
    Public FrmDownloadResourcePack As PageDownloadResourcePack

    '设置页面声明
    Public FrmSetupLeft As PageSetupLeft
    Public FrmSetupLaunch As PageSetupLaunch
    Public FrmSetupUI As PageSetupUI
    Public FrmSetupSystem As PageSetupSystem
    Public FrmSetupLink As PageSetupLink

    '其他页面声明
    Public FrmOtherLeft As PageOtherLeft
    Public FrmOtherAbout As PageOtherAbout
    Public FrmOtherTools As PageOtherTools
    Public FrmColorText As PageColorText
    Public FrmOreGuide As PageOreGuide
    Public FrmGameStats As PageGameStats
    Public FrmCommandQuery As PageCommandQuery
    Public FrmToolsLeft As PageToolsLeft

    '登录页面声明
    Public FrmLoginLegacy As PageLoginLegacy
    Public FrmLoginNide As PageLoginNide
    Public FrmLoginNideSkin As PageLoginNideSkin
    Public FrmLoginAuth As PageLoginAuth
    Public FrmLoginAuthSkin As PageLoginAuthSkin
    Public FrmLoginMs As PageLoginMs
    Public FrmLoginMsSkin As PageLoginMsSkin

    '版本设置页面声明
    Public FrmInstanceLeft As PageInstanceLeft
    Public FrmInstanceOverall As PageInstanceOverall
    Public FrmInstanceMod As PageInstanceMod
    Public FrmInstanceModDisabled As PageInstanceModDisabled
    Public FrmInstanceSetup As PageInstanceSetup
    Public FrmInstanceExport As PageInstanceExport
    Public FrmDownloadModify As PageDownloadModify

    '资源信息分页声明
    Public FrmDownloadResourceDetail As PageDownloadResourceDetail

#End Region

#Region "愚人节"

    Private _IsAprilEnabled As Boolean? = Nothing
    Public ReadOnly Property IsAprilEnabled As Boolean
        Get
            If _IsAprilEnabled Is Nothing Then
                _IsAprilEnabled =
                    Date.Now.Month = 4 AndAlso Date.Now.Day = 1 AndAlso
                    Settings.Get(Of Integer)("AprilYear") <> Date.Now.Year '成功后同年内则不再触发
            End If
            Return _IsAprilEnabled.Value
        End Get
    End Property
    Public IsAprilGiveup As Boolean = False
    Private AprilDifficultiedDistance As Integer = 0
    Private Sub TimerFool()
        Try
            If FrmLaunchLeft Is Nothing OrElse FrmLaunchLeft.AprilPosTrans Is Nothing OrElse FrmMain.lastMouseArg Is Nothing Then Return
            If IsAprilGiveup OrElse FrmMain.PageCurrent <> FormMain.PageType.Launch OrElse AniControlEnabled <> 0 OrElse FrmLaunchLeft Is Nothing OrElse Not FrmLaunchLeft.BtnLaunch.IsLoaded Then Return

            '计算是否空闲
            Dim MousePos = FrmMain.lastMouseArg.GetPosition(FrmMain)
            Static IdieCount As Integer = 0
            Static MousePosLast As New Point(0, 0)
            If MousePos = MousePosLast Then
                IdieCount += 1
            Else
                MousePosLast = MousePos
                IdieCount = 0
            End If
            '计算躲避移动
            Dim Direction As Vector
            Dim Distance As Double
            Dim ButtonWidth = FrmLaunchLeft.BtnLaunch.ActualWidth / 2, ButtonHeight = FrmLaunchLeft.BtnLaunch.ActualHeight / 2
            Dim Vec As Vector = FrmMain.lastMouseArg.GetPosition(FrmLaunchLeft.BtnLaunch) - New Vector(ButtonWidth, ButtonHeight)
            Dim Dir As New Vector(Vec.X, Vec.Y)
            Dir.Normalize()
            Direction = -Dir
            Distance = New Vector(Math.Max(0, Math.Abs(Vec.X) - ButtonWidth), Math.Max(0, Math.Abs(Vec.Y) - ButtonHeight)).Length
            Dim BreathScale = Math.Sin(Timer150Count / 37.5 * Math.PI)
            Dim Difficulty As Double = 1 / (1 + (AprilDifficultiedDistance / 6000) ^ 2) + 0.25 '难度，初始为 1.25x，6000 距离为 0.75x，12000 为 0.5x，24000 为 0.256x，最低 0.25x
            Dim Acc = Math.Max(0, -0.65 - Math.Log((Distance / Difficulty + 0.4) / 200)) * Direction * Difficulty '加速度
            'FrmMain.Title = CInt(AprilTotalDistance) & " - " & Difficulty
            '5s 不动时回到起始点
            If IdieCount >= 64 * 5 Then
                Dim SafeDist As Vector = FrmMain.lastMouseArg.GetPosition(FrmMain.PanMain) - New Vector(ButtonWidth, FrmMain.PanMain.ActualHeight - ButtonHeight * 3)
                Dim Back As New Vector(FrmLaunchLeft.AprilPosTrans.X, FrmLaunchLeft.AprilPosTrans.Y)
                If SafeDist.Length > 250 AndAlso Back.Length > 0.4 Then
                    Acc -= Back * 0.0005
                    Back.Normalize()
                    Acc -= Back * 0.15
                End If
            End If
            '回到边界
            Static Speed As New Vector(0, 0)
            Dim Relative As Point = FrmLaunchLeft.BtnLaunch.TranslatePoint(New Point(0, 0), FrmMain.PanForm)
            If Relative.X < -ButtonWidth * 2 Then
                FrmLaunchLeft.AprilPosTrans.X += FrmMain.PanForm.ActualWidth + ButtonWidth * 2 '离开左边界
                Speed.X -= 80
                If Relative.Y < 0 Then
                    FrmLaunchLeft.AprilPosTrans.Y += ButtonHeight * 2.5
                ElseIf Relative.Y > FrmMain.PanForm.ActualHeight - ButtonHeight * 2 Then
                    FrmLaunchLeft.AprilPosTrans.Y -= ButtonHeight * 2.5
                End If
            ElseIf Relative.X > FrmMain.PanForm.ActualWidth Then
                FrmLaunchLeft.AprilPosTrans.X -= FrmMain.PanForm.ActualWidth + ButtonWidth * 2 '离开右边界
                Speed.X += 80
                If Relative.Y < 0 Then
                    FrmLaunchLeft.AprilPosTrans.Y += ButtonHeight * 2.5
                ElseIf Relative.Y > FrmMain.PanForm.ActualHeight - ButtonHeight * 2 Then
                    FrmLaunchLeft.AprilPosTrans.Y -= ButtonHeight * 2.5
                End If
            ElseIf Relative.Y < -ButtonHeight * 2 Then
                FrmLaunchLeft.AprilPosTrans.Y += FrmMain.PanForm.ActualHeight + ButtonHeight * 2 '离开上边界
                Speed.Y -= 25
                If Relative.X < 0 Then
                    FrmLaunchLeft.AprilPosTrans.X += ButtonWidth * 2
                ElseIf Relative.X > FrmMain.PanForm.ActualWidth - ButtonWidth * 2 Then
                    FrmLaunchLeft.AprilPosTrans.X -= ButtonWidth * 2
                End If
            ElseIf Relative.Y > FrmMain.PanForm.ActualHeight Then
                FrmLaunchLeft.AprilPosTrans.Y -= FrmMain.PanForm.ActualHeight + ButtonHeight * 2 '离开下边界
                Speed.Y += 25
                If Relative.X < 0 Then
                    FrmLaunchLeft.AprilPosTrans.X += ButtonWidth * 2
                ElseIf Relative.X > FrmMain.PanForm.ActualWidth - ButtonWidth * 2 Then
                    FrmLaunchLeft.AprilPosTrans.X -= ButtonWidth * 2
                End If
            End If
            '移动
            Speed = Speed * 0.8 + Acc
            Dim SpeedValue = Math.Min(60, Speed.Length)
            If SpeedValue < 0.01 Then Return
            Speed.Normalize()
            Speed *= SpeedValue
            AprilDifficultiedDistance += SpeedValue
            FrmLaunchLeft.AprilPosTrans.X += Speed.X
            FrmLaunchLeft.AprilPosTrans.Y += Speed.Y
            '大小改变
            FrmLaunchLeft.AprilScaleTrans.ScaleX = (1 - (Math.Abs(Direction.X) - Math.Abs(Direction.Y)) * (SpeedValue / 160)).Clamp(0.2, 1.8)
            FrmLaunchLeft.AprilScaleTrans.ScaleY = (1 - (Math.Abs(Direction.Y) - Math.Abs(Direction.X)) * (SpeedValue / 100)).Clamp(0.2, 1.8)
            '放弃提示
            Static GiveUpDistance As Double = -1500
            GiveUpDistance += SpeedValue
            If GiveUpDistance > 2500 Then
                GiveUpDistance = 0
                Select Case RandomInteger(0, 3)
                    Case 0
                        Hint("放弃吧！只需要点一下右下角的小白旗……", HintType.Red)
                    Case 1
                        Hint("看到右下角的那面小白旗了吗？", HintType.Red)
                    Case 2
                        Hint("这里建议点一下右下角的小白旗投降呢.jpg", HintType.Red)
                    Case 3
                        Hint("右下角的小白旗永远等着你……", HintType.Red)
                End Select
            End If

        Catch ex As Exception
            Logger.Error(ex, "愚人节移动出错")
        End Try
    End Sub

#End Region

#Region "系统"

    ''' <summary>
    ''' 把某个缘启窗口拖到最前面。
    ''' </summary>
    Public Sub ShowWindowToTop(Handle As IntPtr)
        Try
            If Handle = IntPtr.Zero Then Return
            '关键：先用系统级还原/显示，再通知窗口内部做工作。
            '只发消息的话，窗口处于最小化时第一次点击经常只响系统提示音而窗口不出现。
            RestoreWindowNative(Handle)
            PostMessage(Handle, 400 * 16 + 2, 0, 0)
            SetForegroundWindow(Handle) '不在这里放不行，神秘 WinAPI，建议别动
        Catch ex As Exception
            Logger.Error(ex, "设置窗口置顶失败", LogBehavior.Toast)
        End Try
    End Sub

    ''' <summary>
    ''' 系统级还原并显示窗口。
    ''' 只做 SHOW/RESTORE，不发自定义消息——否则会与窗口内处理 "置顶请求" 的逻辑形成递归。
    ''' </summary>
    Public Sub RestoreWindowNative(Handle As IntPtr)
        Try
            If Handle = IntPtr.Zero Then Return
            If IsIconic(Handle) Then
                Logger.Info("目标窗口处于最小化，先执行系统级还原")
                ShowWindow(Handle, SwRestore)
            End If
            ShowWindow(Handle, SwShow)
        Catch ex As Exception
            Logger.Warn(ex, "系统级还原窗口失败")
        End Try
    End Sub
    Public Declare Function FindWindow Lib "user32" Alias "FindWindowA" (ClassName As String, WindowName As String) As IntPtr
    Public Declare Function SetForegroundWindow Lib "user32" (hWnd As IntPtr) As Integer
    Private Declare Function PostMessage Lib "user32" Alias "PostMessageA" (hWnd As IntPtr, msg As UInteger, wParam As Long, lParam As Long) As Boolean
    Private Declare Function ShowWindow Lib "user32" (hWnd As IntPtr, nCmdShow As Integer) As Boolean
    Private Declare Function IsIconic Lib "user32" (hWnd As IntPtr) As Boolean
    Private Const SwRestore As Integer = 9
    Private Const SwShow As Integer = 5

    ''' <summary>允许指定进程抢前台（传 -1 表示不限进程）。启动游戏前调用，游戏窗口才能自己跑到最前面。</summary>
    Public Declare Function AllowSetForegroundWindow Lib "user32" (dwProcessId As Integer) As Boolean

    Private Declare Function SetWindowPos Lib "user32" (hWnd As IntPtr, hWndInsertAfter As IntPtr, X As Integer, Y As Integer,
                                                        cx As Integer, cy As Integer, uFlags As UInteger) As Boolean
    Private Declare Function GetWindowLongW Lib "user32" (hWnd As IntPtr, nIndex As Integer) As Integer

    ''' <summary>窗口当前是否处于置顶（WS_EX_TOPMOST）。</summary>
    Public Function IsWindowTopmost(Handle As IntPtr) As Boolean
        Try
            If Handle = IntPtr.Zero Then Return False
            '32 位进程没有 GetWindowLongPtr 导出，用 GetWindowLong 取扩展样式就够了
            Return (GetWindowLongW(Handle, -20) And &H8) <> 0
        Catch
            Return False
        End Try
    End Function

    Private Const SwpNoSize As UInteger = &H1
    Private Const SwpNoMove As UInteger = &H2
    Private Const SwpNoActivate As UInteger = &H10
    Private ReadOnly HwndTopmost As IntPtr = New IntPtr(-1)
    Private ReadOnly HwndNoTopmost As IntPtr = New IntPtr(-2)

    ''' <summary>
    ''' 用 Win32 直接设置/取消窗口置顶。
    ''' 不要用 WPF 的 Topmost = True 再 Topmost = False —— 同一帧的两次赋值会被合并，
    ''' 偶尔让窗口永久停在最上层（表现就是启动器一直盖住游戏窗口）。
    ''' </summary>
    Public Sub SetWindowTopmost(Handle As IntPtr, OnTop As Boolean)
        Try
            If Handle = IntPtr.Zero Then Return
            Dim Ok As Boolean = SetWindowPos(Handle, If(OnTop, HwndTopmost, HwndNoTopmost), 0, 0, 0, 0, SwpNoMove Or SwpNoSize Or SwpNoActivate)
            If Not Ok Then Logger.Warn($"设置窗口层级失败：OnTop={OnTop} hwnd={Handle} 错误码={Runtime.InteropServices.Marshal.GetLastWin32Error()}")
        Catch ex As Exception
            Logger.Warn(ex, "设置窗口层级失败")
        End Try
    End Sub

    ''' <summary>
    ''' 将特定程序设置为使用高性能显卡启动。
    ''' 如果失败，则抛出异常。
    ''' </summary>
    Public Sub SetGPUPreference(Executeable As String)
        Const REG_KEY As String = "HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences"
        Const REG_VALUE As String = "GpuPreference=2;"
        '查看现有设置
        If RegistryUtils.Read(REG_KEY, Executeable).result?.ToString() = REG_VALUE Then
            Logger.Info($"无需调整显卡设置：{Executeable}")
            Return
        End If
        '写入新设置
        RegistryUtils.Write(REG_KEY, Executeable, REG_VALUE)
        Logger.Info($"已调整显卡设置：{Executeable}")
    End Sub

    ''' <summary>
    ''' 对替换标记进行处理。会对替换内容使用 EscapeHandler 进行转义。
    ''' </summary>
    Public Function ArgumentReplace(Text As String, Optional EscapeHandler As Func(Of String, String) = Nothing, Optional ReplaceTime As Boolean = True) As String
        If Text Is Nothing OrElse Not Text.Contains("{") Then Return Text
        '预处理（注意，文件夹必须以 \ 结尾）
        Static Replacer As Func(Of String, String) =
        Function(s As String) As String
            If s Is Nothing Then Return ""
            If EscapeHandler Is Nothing Then Return s
            If s.Contains(":\") Then
                Dim IsFolder = s.EndsWithF("\") OrElse s.EndsWithF("/")
                s = PathUtils.ToShortPath(s)
                If IsFolder Then s = PathUtils.AddSlashSuffix(s)
            End If
            Return EscapeHandler(s)
        End Function
        '基础
        Text = Text.Replace("{pcl_version}", Replacer(VersionBaseName))
        Text = Text.Replace("{pcl_version_code}", Replacer(VersionCode))
        Text = Text.Replace("{pcl_version_branch}", Replacer(BuildTypeDisplay))
        Text = Text.Replace("{pcl_build_type}", Replacer(BuildType.ToString))
        Text = Text.Replace("{pcl_branch}", Replacer(VersionBranchMain))
        Text = Text.Replace("{identify}", Replacer(Identify))
        Text = Text.Replace("{path}", Replacer(Paths.Base))
        Text = Text.Replace("{path_with_name}", Replacer(PathExe))
        Text = Text.Replace("{path_temp}", Replacer(PathTemp))
        Text = Text.Replace("{pcl_md5}", Function() Replacer(CryptographyUtils.ComputeFileHash(PathExe, CryptographyUtils.HashMethod.Md5)))
        Text = Text.Replace("{pcl_sha1}", Function() Replacer(CryptographyUtils.ComputeFileHash(PathExe, CryptographyUtils.HashMethod.Sha1)))
        '时间
        If ReplaceTime Then '在窗口标题中，时间会被后续动态替换，所以此时不应该替换
            Text = Text.Replace("{date}", Replacer(Date.Now.ToString("yyyy'/'M'/'d")))
            Text = Text.Replace("{time}", Replacer(Date.Now.ToString("HH':'mm':'ss")))
        End If
        'Minecraft
        Text = Text.Replace("{java}", Replacer(McLaunchJavaSelected?.Folder))
        Text = Text.Replace("{minecraft}", Replacer(McFolderSelected))
        If McInstanceSelected?.IsLoaded Then
            Text = Text.Replace("{version_path}", Replacer(McInstanceSelected.PathVersion)) : Text = Text.Replace("{verpath}", Replacer(McInstanceSelected.PathVersion))
            Text = Text.Replace("{version_indie}", Replacer(McInstanceSelected.PathIndie)) : Text = Text.Replace("{verindie}", Replacer(McInstanceSelected.PathIndie))
            Text = Text.Replace("{name}", Replacer(McInstanceSelected.Name))
            If {"unknown", "old", "pending"}.Contains(McInstanceSelected.Version.VanillaName.Lower) Then
                Text = Text.Replace("{version}", Replacer(McInstanceSelected.Name))
            Else
                Text = Text.Replace("{version}", Replacer(McInstanceSelected.Version.VanillaName))
            End If
            Text = Text.Replace("{forge}", Replacer(If(McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.Forge), McInstanceSelected.Version.ModLoaderVersions(ModLoaders.Forge), "")))
            Text = Text.Replace("{fabric}", Replacer(If(McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.Fabric), McInstanceSelected.Version.ModLoaderVersions(ModLoaders.Fabric), "")))
            Text = Text.Replace("{neoforge}", Replacer(If(McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge), McInstanceSelected.Version.ModLoaderVersions(ModLoaders.NeoForge), "")))
            Text = Text.Replace("{liteloader}", Replacer(If(McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader), "true", "")))
        Else
            Text = Text.Replace("{version_path}", Replacer(Nothing)) : Text = Text.Replace("{verpath}", Replacer(Nothing))
            Text = Text.Replace("{version_indie}", Replacer(Nothing)) : Text = Text.Replace("{verindie}", Replacer(Nothing))
            Text = Text.Replace("{name}", Replacer(Nothing))
            Text = Text.Replace("{version}", Replacer(Nothing))
            Text = Text.Replace("{forge}", Replacer(Nothing))
            Text = Text.Replace("{fabric}", Replacer(Nothing))
            Text = Text.Replace("{neoforge}", Replacer(Nothing))
            Text = Text.Replace("{liteloader}", Replacer(Nothing))
        End If
        '登录信息
        If McLoginLoader.State = LoadState.Finished Then
            Text = Text.Replace("{user}", Replacer(McLoginLoader.Output.Name))
            Text = Text.Replace("{uuid}", Replacer(McLoginLoader.Output.Uuid?.Lower))
            Select Case McLoginLoader.Input.Type
                Case McLoginType.Legacy
                    Text = Text.Replace("{login}", Replacer("离线"))
                Case McLoginType.Ms
                    Text = Text.Replace("{login}", Replacer("正版"))
                Case McLoginType.Nide
                    Text = Text.Replace("{login}", Replacer("统一通行证"))
                Case McLoginType.Auth
                    Text = Text.Replace("{login}", Replacer("Authlib-Injector"))
            End Select
        Else
            Text = Text.Replace("{user}", Replacer(Nothing))
            Text = Text.Replace("{uuid}", Replacer(Nothing))
            Text = Text.Replace("{login}", Replacer(Nothing))
        End If
        '高级
        Text = Text.RegexReplace("\{hint\}", Function() Replacer(PageOtherTest.GetRandomHint()))
        Text = Text.RegexReplace("\{cave\}", Function() Replacer(PageOtherTest.GetRandomCave()))
        Text = Text.RegexReplace("\{setup:([a-zA-Z0-9]+)\}", Function(m) Replacer(Settings.GetSafe(m.Groups(1).Value, McInstanceSelected)))
        Text = Text.RegexReplace("\{varible:([^:\}]+)(?::([^\}]+))?\}", Function(m) Replacer(RegistryUtils.TryRead($"HKEY_CURRENT_USER\Software\{RegFolder}", $"CustomEvent{m.Groups(1).Value}", m.Groups(2).Value)))
        Text = Text.RegexReplace("\{variable:([^:\}]+)(?::([^\}]+))?\}", Function(m) Replacer(RegistryUtils.TryRead($"HKEY_CURRENT_USER\Software\{RegFolder}", $"CustomEvent{m.Groups(1).Value}", m.Groups(2).Value)))
        Return Text
    End Function

#End Region

#Region "任务缓存"

    Private IsTaskTempCleared As Boolean = False
    Private IsTaskTempClearing As Boolean = False

    ''' <summary>
    ''' 尝试清理任务缓存文件夹。
    ''' 在整次运行中只会实际清理一次。
    ''' </summary>
    Public Sub TryClearTaskTemp()
        If Not IsTaskTempCleared Then
            IsTaskTempCleared = True
            IsTaskTempClearing = True
            Try
                Logger.Info("开始清理任务缓存文件夹")
                DirectoryUtils.Delete($"{OsDrive}ProgramData\YQL\TaskTemp\")
                DirectoryUtils.Delete($"{PathTemp}TaskTemp\")
                Logger.Info("已清理任务缓存文件夹")
            Catch ex As Exception
                Logger.Warn(ex, "清理任务缓存文件夹失败")
            Finally
                IsTaskTempClearing = False
            End Try
        ElseIf IsTaskTempClearing Then
            '等待另一个清理步骤完成
            Do While IsTaskTempClearing
                Thread.Sleep(1)
            Loop
        End If
    End Sub

    ''' <summary>
    ''' 申请一个可用于任务缓存的临时文件夹，以 \ 结尾。这些文件夹无需进行后续清理。
    ''' 若所有缓存位置均没有权限，会抛出异常。
    ''' </summary>
    ''' <param name="RequireNonSpace">是否要求路径不包含空格。</param>
    Public Function RequestTaskTempFolder(Optional RequireNonSpace As Boolean = False) As String
        TryClearTaskTemp()
        Dim ResultFolder As String
        Try
            ResultFolder = $"{PathTemp}TaskTemp\{GetUuid()}-{RandomInteger(0, 1000000)}\"
            If RequireNonSpace AndAlso ResultFolder.Contains(" ") Then Exit Try '带空格
            DirectoryUtils.Create(ResultFolder)
            CheckPermissionWithException(ResultFolder)
            Return ResultFolder
        Catch
        End Try
        '使用备用路径
        ResultFolder = $"{OsDrive}ProgramData\YQL\TaskTemp\{GetUuid()}-{RandomInteger(0, 1000000)}\"
        DirectoryUtils.Create(ResultFolder)
        CheckPermission(ResultFolder)
        Return ResultFolder
    End Function

#End Region

#Region "反馈与遥测"


    ''' <summary>
    ''' 在日志中输出系统诊断信息。
    ''' </summary>
    Public Sub FeedbackInfo()
        On Error Resume Next
        Logger.Warn($"诊断信息：{vbCrLf}" &
            "操作系统：" & My.Computer.Info.OSFullName & vbCrLf &
            "剩余内存：" & Int(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024) & " M / " & Int(My.Computer.Info.TotalPhysicalMemory / 1024 / 1024) & " M" & vbCrLf &
            "DPI：" & DPI & "（" & Math.Round(DPI / 96, 2) * 100 & "%）" & vbCrLf &
            "MC 文件夹：" & If(McFolderSelected, "Nothing") & vbCrLf &
            "文件位置：" & Paths.Base)
    End Sub

    '遥测（缘启不进行任何数据收集）
    Public Sub Telemetry([Event] As String, ParamArray Datas As String())
    End Sub

#End Region

    Public DragControl = Nothing
    Private Timer4Count As Integer = 0
    Private Timer150Count As Integer = 0
    Private Sub TimerMain()
        Try
#Region "每 50ms 执行一次的代码"
            HintTick()
            MyMsgBoxTick()
            FrmMain.DragTick()
            LoaderTaskbarProgressRefresh()
            If ThemeDontClick = 2 Then ThemeRefresh()
#End Region
        Catch ex As Exception
            Logger.Error(ex, "短程主时钟执行异常", LogBehavior.AlertThenCrash)
        End Try
        Timer4Count += 1
        If Timer4Count = 4 Then
            Timer4Count = 0
            Try
#Region "每 250ms 执行一次的代码"
                If ThemeNow = 12 Then ThemeRefresh()
#End Region
            Catch ex As Exception
                Logger.Warn(ex, "中程主时钟执行异常")
            End Try
        End If
        Timer150Count += 1
        If Timer150Count = 150 Then
            Timer150Count = 0
            Try
#Region "每 7.5s 执行一次的代码"
                If FrmMain.BtnExtraApril_ShowCheck AndAlso AprilDifficultiedDistance <> 0 Then FrmMain.BtnExtraApril.Ribble()
                '以未知原因窗口被丢到一边去的修复（Top、Left = -25600），还有 #745
                RunInUi(
                Sub()
                    If Not FrmMain.Hidden Then
                        If FrmMain.Top < -9000 Then FrmMain.Top = 100
                        If FrmMain.Left < -9000 Then FrmMain.Left = 100 '窗口拉至最大时 Left = -18.8
                    End If
                End Sub)
#End Region
            Catch ex As Exception
                Logger.Error(ex, "长程主时钟执行异常", LogBehavior.AlertThenCrash)
            End Try
        End If
    End Sub
    Public Sub TimerMainStart()
        RunInNewThread(
        Sub()
            Try
                Do While True
                    RunInUiWait(AddressOf TimerMain)
                    Thread.Sleep(50 * 0.98)
                Loop
            Catch ex As Exception
                Logger.Error(ex, "程序主时钟出错")
            End Try
        End Sub, "Timer Main")
        If Not IsAprilEnabled Then Return
        RunInNewThread(
        Sub()
            Try
                Dim LastTime = My.Computer.Clock.TickCount
                Do While True
                    If LastTime <> My.Computer.Clock.TickCount Then
                        LastTime = My.Computer.Clock.TickCount
                        RunInUiWait(AddressOf TimerFool)
                    End If
                    Thread.Sleep(1)
                Loop
            Catch ex As Exception
                Logger.Error(ex, "愚人节主时钟出错")
            End Try
        End Sub, "Timer Main Fool")
    End Sub

End Module
