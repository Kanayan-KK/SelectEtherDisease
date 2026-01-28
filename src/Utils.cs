namespace SelectEtherDisease
{
    // NOTE:可読性を上げるために処理を外部化
    internal class Utils
    {
        /// <summary>
        /// 対象キャラのエーテル病更新処理
        /// </summary>
        public static void ApplyEther(Chara c, SourceElement.Row row, int vec)
        {
            // 対象エーテル病の罹患状態を取得
            var element = c.elements.GetElement(row.id);

            // エーテル病進行度
            var num = 1;

            // 初感染ではない時は進行度変数を更新
            if (element != null)
                num = element.Value + vec;

            // テキストログ表示 (感染時のみ)
            if (vec > 0)
                c.Say("mutation_gain", c);

            // フィート設定
            c.SetFeat(row.id, num);

            // エーテル病罹患履歴がnullの時（初感染）は作成
            c.c_corruptionHistory ??= [];

            // エーテル病罹患履歴に追記 (感染時のみ)
            if (vec > 0)
            {
                // Source: Chara.cs L-10137
                c.c_corruptionHistory.Add(row.id);
            }

            // 更新された罹患状態を取得
            var updatedElement = c.elements.GetElement(row.id);

            // 感染時のみポップアップテキストを表示
            if (vec > 0)
            {
                // Source: Chara.cs L-10141
                WidgetPopText.Say("popEther".lang(updatedElement.Name, c.Name));
            }

            // エーテル病初感染時のイベント対応処理
            if (c.IsPC && vec > 0)
            {
                // チュートリアル予約（ネルンのイベント）
                // Source: Chara.cs L-9945 (in ModCorruption)
                Tutorial.Reserve("ether");

                // 手紙と抗体の小包イベント
                // Source: Chara.cs L-10143
                if (!EClass.player.flags.gotEtherDisease)
                {
                    EClass.player.flags.gotEtherDisease = true;
                    var thing = ThingGen.Create("parchment");
                    thing.SetStr(53, "letter_ether");
                    var thing2 = ThingGen.Create("1165");
                    thing2.SetBlessedState(BlessedState.Normal);
                    var p = ThingGen.CreateParcel(null, thing2, thing);
                    EClass.world.SendPackage(p);
                }
            }

            // エフェクト再生とテキストログ表示
            if (EClass.core.IsGameStarted && c.pos != null)
            {
                c.PlaySound("mutation_ether");
                c.PlayEffect("mutation");

                // 感染時と治療時でテキストカラーを変える
                Msg.SetColor(vec > 0 ? Msg.colors.MutateBad : Msg.colors.MutateGood);

                // 感染時と治療時で表示テキストを変える
                // Source: Chara.cs L-10157
                var msgKey = (vec > 0) ? "textDec" : "textInc";
                c.Say(row.GetText(msgKey, returnNull: true) ?? row.alias, c);
            }
        }
    }
}