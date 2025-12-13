namespace SelectEtherDisease
{
    // 可読性を上げるために処理を外部化
    internal class Utils
    {
        // 対象キャラにエーテル病を発病させる処理
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
                c.c_corruptionHistory.Add(row.id);

            // 更新された罹患状態を取得
            var updatedElement = c.elements.GetElement(row.id);

            // 感染時のみポップアップテキストを表示
            if (vec > 0)
                WidgetPopText.Say("popEther".lang(updatedElement.Name, c.Name));

            // エーテル病初感染時の手紙イベント対応処理
            if (c.IsPC && !EClass.player.flags.gotEtherDisease)
            {
                EClass.player.flags.gotEtherDisease = true;
                var thing = ThingGen.Create("parchment");
                thing.SetStr(53, "letter_ether");
                var thing2 = ThingGen.Create("1165");
                thing2.SetBlessedState(BlessedState.Normal);
                var p = ThingGen.CreateParcel(null, thing2, thing);
                EClass.world.SendPackage(p);
            }

            // エフェクト再生とテキストログ表示
            if (EClass.core.IsGameStarted && c.pos != null)
            {
                c.PlaySound("mutation_ether");
                c.PlayEffect("mutation");

                // 感染時と治療時でテキストカラーを変える
                Msg.SetColor(vec > 0 ? Msg.colors.MutateBad : Msg.colors.MutateGood);

                // 感染時と治療時で表示テキストを変える
                var msgKey = (vec > 0) ? "textDec" : "textInc";
                c.Say(row.GetText(msgKey, returnNull: true) ?? row.alias, c);
            }
        }
    }
}