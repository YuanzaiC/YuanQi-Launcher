Public Class PageOtherTools

    Private Sub PageOtherTools_Loaded() Handles Me.Loaded
        If PanCards Is Nothing Then Return
        '挂载处于切页动画的 AniControlEnabled 抑制窗口内：入场动画延迟 30ms 触发，每次进入都播放
        AniStart({AaCode(Sub() AnimateCardsEnter(), 30, True)}, "PageOtherTools Enter Trigger")
    End Sub

        ' <summary>工具卡依次淡入上移。</summary>
    Private Sub AnimateCardsEnter()
        Try
            If AniControlEnabled <> 0 Then Return
            Dim Anims As New List(Of AniData)
            Dim i As Integer = 0
            For Each Child As UIElement In PanCards.Children
                Child.Opacity = 0
                Child.RenderTransform = New TranslateTransform(0, 16)
                Anims.Add(AaOpacity(Child, 1, 240, 60 + i * 55, New AniEaseOutFluent(AniEasePower.Weak)))
                Anims.Add(AaTranslateY(Child, -16, 320, 60 + i * 55, New AniEaseOutFluent(AniEasePower.Weak)))
                i += 1
            Next
            AniStart(Anims.ToArray(), "PageOtherTools Enter")
        Catch ex As Exception
            Logger.Warn(ex, "工具页入场动画失败")
        End Try
    End Sub

    Private Sub BtnJrrp_Click() Handles BtnJrrp.Click
        PageOtherTest.Jrrp()
    End Sub

    Private Sub BtnMemory_Click() Handles BtnMemory.Click
        RunInThread(Sub() PageOtherTest.MemoryOptimize(True))
    End Sub

    Private Sub BtnClear_Click() Handles BtnClear.Click
        RunInThread(Sub() PageOtherTest.RubbishClear())
    End Sub

    Private Sub BtnLog_Click() Handles BtnLog.Click
        OpenExplorer(Paths.Base & "YQL\Log1.txt")
    End Sub

End Class
