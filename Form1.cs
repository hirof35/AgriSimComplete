using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.Json;
using System.Windows.Forms;

namespace AgriSimComplete
{
    public partial class Form1 : Form
    {
        // --- データ構造 ---
        public enum Season { 春, 夏, 秋, 冬 }

        public class Crop
        {
            public string Name { get; set; }
            public int Current { get; set; }
            public int Max { get; set; }
            public int Seed { get; set; }
            public int Sell { get; set; }
            public Crop() { }
            public Crop(string n, int m, int sd, int sl) { Name = n; Max = m; Seed = sd; Sell = sl; }
        }

        public class Plot
        {
            public int ID { get; set; }
            public Crop Planted { get; set; }
            public bool IsSick { get; set; }
            [System.Text.Json.Serialization.JsonIgnore]
            public Button PlotButton { get; set; }
        }

        public class SaveData
        {
            public int Money { get; set; }
            public int TotalEarned { get; set; }
            public int Day { get; set; }
            public int Fertilizer { get; set; }
            public int Pesticide { get; set; }
            public List<Plot> Fields { get; set; }
        }

        // --- 管理変数 ---
        List<Plot> farmField = new List<Plot>();
        List<Crop> seedCatalog = new List<Crop>();
        int money = 1000, totalEarned = 0, currentDay = 1;
        int fertilizerCount = 0, pesticideCount = 0;
        Season currentSeason = Season.春;
        Random rand = new Random();

        // UI & Sound
        FlowLayoutPanel fieldPanel = new FlowLayoutPanel();
        Label lblInfo = new Label();
        ComboBox cmbSeeds = new ComboBox();
        TextBox txtLog = new TextBox();
        SoundPlayer harvestSound, bgmPlayer;

        public Form1()
        {
            this.Text = "究極のC#農業シミュレータ - 完成版";
            this.Size = new Size(850, 750);
            this.DoubleBuffered = true;

            InitData();
            SetupUI();
            InitSound();
            AddLog("--- 経営開始！100日後の農聖を目指しましょう ---");
            RefreshDisplay();
        }

        private void InitData()
        {
            seedCatalog.Add(new Crop("カイワレ", 1, 50, 150));
            seedCatalog.Add(new Crop("キャベツ", 3, 100, 500));
            seedCatalog.Add(new Crop("トマト", 5, 200, 1200));
            for (int i = 1; i <= 5; i++) AddPlot();
        }

        private void SetupUI()
        {
            lblInfo.Location = new Point(20, 10);
            lblInfo.Font = new Font("メイリオ", 11, FontStyle.Bold);
            lblInfo.AutoSize = true;
            this.Controls.Add(lblInfo);

            fieldPanel.Location = new Point(20, 50);
            fieldPanel.Size = new Size(790, 280);
            fieldPanel.AutoScroll = true;
            fieldPanel.BackColor = Color.SaddleBrown;
            this.Controls.Add(fieldPanel);

            GroupBox grp = new GroupBox { Text = "農作業・ショップ", Location = new Point(20, 340), Size = new Size(790, 110) };
            this.Controls.Add(grp);

            cmbSeeds.Items.AddRange(new string[] { "カイワレ(50円)", "キャベツ(100円)", "トマト(200円)" });
            cmbSeeds.SelectedIndex = 0; cmbSeeds.Location = new Point(10, 30); grp.Controls.Add(cmbSeeds);

            // アクションボタン
            AddButton(grp, "1日進める", 140, 25, Color.LightYellow, (s, e) => NextDay());
            AddButton(grp, "肥料購入(150円)", 250, 25, Color.White, (s, e) => { if (Buy(150)) fertilizerCount++; });
            AddButton(grp, "農薬購入(100円)", 360, 25, Color.White, (s, e) => { if (Buy(100)) pesticideCount++; });
            AddButton(grp, "土地拡張(5000円)", 470, 25, Color.Orange, (s, e) => { if (Buy(5000)) AddPlot(); });
            AddButton(grp, "セーブ", 600, 25, Color.LightGray, (s, e) => SaveGame());

            txtLog.Location = new Point(20, 460);
            txtLog.Size = new Size(790, 230);
            txtLog.Multiline = true; txtLog.ReadOnly = true; txtLog.ScrollBars = ScrollBars.Vertical;
            this.Controls.Add(txtLog);
        }

        private void AddButton(Control p, string text, int x, int y, Color c, EventHandler ev)
        {
            Button b = new Button { Text = text, Location = new Point(x, y), Size = new Size(100, 40), BackColor = c };
            b.Click += ev; p.Controls.Add(b);
        }

        private void AddPlot()
        {
            var p = new Plot { ID = farmField.Count + 1 };
            var btn = new Button { Size = new Size(140, 120), Margin = new Padding(5), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Emoji", 20) };
            btn.Click += (s, e) => OnPlotClick(p);
            p.PlotButton = btn; farmField.Add(p); fieldPanel.Controls.Add(btn);
            RefreshDisplay();
        }

        private void OnPlotClick(Plot p)
        {
            if (p == null || p.PlotButton == null) return;

            try
            {
                if (p.IsSick)
                {
                    if (pesticideCount > 0)
                    {
                        pesticideCount--; p.IsSick = false;
                        AddLog($"{p.ID}番の病気を治しました。");
                    }
                }
                else if (p.Planted == null)
                {
                    // 種まき
                    var s = seedCatalog[cmbSeeds.SelectedIndex];
                    if (Buy(s.Seed))
                    {
                        p.Planted = new Crop(s.Name, s.Max, s.Seed, s.Sell);
                        AddLog($"{p.ID}番に{s.Name}を植えました。");
                    }
                }
                else if (p.Planted.Current >= p.Planted.Max)
                {
                    // ★【修正ポイント】収穫前に必要な情報をローカル変数に退避させる★
                    string cropName = p.Planted.Name;
                    int baseSellPrice = p.Planted.Sell;
                    int finalPrice = (int)(baseSellPrice * GetPriceMultiplier(cropName));

                    // 1. 先にデータを消去（これで連打による二重収穫を防ぐ）
                    p.Planted = null;
                    p.IsSick = false;

                    // 2. 退避させた変数を使って演出を行う（p.Plantedがnullでも大丈夫）
                    money += finalPrice;
                    totalEarned += finalPrice;

                    ShakeButton(p.PlotButton);
                    ShowFloatingText(p.PlotButton, $"+{finalPrice}円");

                    if (harvestSound != null) harvestSound.Play();
                    AddLog($"{p.ID}番の{cropName}を収穫！(+{finalPrice}円)");
                }
                else if (fertilizerCount > 0)
                {
                    // 肥料
                    fertilizerCount--;
                    p.Planted.Current += 2;
                    AddLog($"{p.ID}番に肥料を使用。");
                }
            }
            catch (Exception ex)
            {
                // どこでエラーが起きてもアプリが落ちないようにログに出す
                AddLog("⚠️ エラー発生: " + ex.Message);
            }

            // 最後に描画更新
            RefreshDisplay();
        }

        private void NextDay()
        {
            currentDay++;
            UpdateSeason();
            ProcessDisease();
            if (currentDay % 10 == 0) { money -= farmField.Count * 300; AddLog("納税日：固定資産税を支払いました。"); }
            foreach (var p in farmField.Where(x => x.Planted != null && !x.IsSick)) p.Planted.Current++;
            CheckGameEnd();
            RefreshDisplay();
        }

        private void ProcessDisease()
        {
            foreach (var p in farmField.Where(x => x.Planted != null && !x.IsSick))
                if (rand.Next(100) < 5) p.IsSick = true;
            // 感染拡大
            for (int i = 0; i < farmField.Count; i++)
                if (farmField[i].IsSick)
                {
                    if (i > 0 && farmField[i - 1].Planted != null && rand.Next(100) < 20) farmField[i - 1].IsSick = true;
                    if (i < farmField.Count - 1 && farmField[i + 1].Planted != null && rand.Next(100) < 20) farmField[i + 1].IsSick = true;
                }
        }

        private double GetPriceMultiplier(string name)
        {
            if (currentSeason == Season.冬 && name == "トマト") return 2.5;
            if (currentSeason == Season.春 && name == "キャベツ") return 0.5;
            return 1.0;
        }

        private void UpdateSeason()
        {
            currentSeason = (Season)(((currentDay - 1) / 10) % 4);
            switch (currentSeason)
            {
                case Season.春: this.BackColor = Color.MistyRose; break;
                case Season.夏: this.BackColor = Color.LightCyan; break;
                case Season.秋: this.BackColor = Color.Wheat; break;
                case Season.冬: this.BackColor = Color.WhiteSmoke; break;
            }
        }

        private bool Buy(int cost) { if (money >= cost) { money -= cost; return true; } AddLog("資金不足！"); return false; }

        private void RefreshDisplay()
        {
            // 1. 上部ステータスの更新
            lblInfo.Text = $"【{currentSeason}】 {currentDay}日目 | 資金: {money}円 | 累計: {totalEarned}円 | 肥料:{fertilizerCount} 農薬:{pesticideCount}";

            // 2. 全ての畑の状態を走査して描画
            foreach (var p in farmField)
            {
                if (p.PlotButton == null) continue;

                // 状態を判定（上から順に優先順位が高い）
                if (p.IsSick)
                {
                    p.PlotButton.Text = "👾\n病気！";
                    p.PlotButton.BackColor = Color.Purple;
                    p.PlotButton.ForeColor = Color.White;
                }
                else if (p.Planted == null)
                {
                    // ★ここが重要！収穫後はここを通る必要がある
                    p.PlotButton.Text = $"🟫\n({p.ID}番:空)";
                    p.PlotButton.BackColor = Color.BurlyWood;
                    p.PlotButton.ForeColor = Color.Black;
                }
                else if (p.Planted.Current >= p.Planted.Max)
                {
                    p.PlotButton.Text = "✨📦✨\n収穫OK";
                    p.PlotButton.BackColor = Color.Gold;
                    p.PlotButton.ForeColor = Color.Black;
                }
                else
                {
                    p.PlotButton.Text = $"🌱\n{p.Planted.Name}\n({p.Planted.Current}/{p.Planted.Max})";
                    p.PlotButton.BackColor = Color.LightGreen;
                    p.PlotButton.ForeColor = Color.Black;
                }

                // 強制的に再描画させる（これを入れると確実です）
                p.PlotButton.Invalidate();
                p.PlotButton.Update();
            }
        }

        private void CheckGameEnd()
        {
            if (money < -5000 || currentDay > 100)
            {
                MessageBox.Show($"ゲーム終了！最終スコア: {totalEarned}点", "リザルト");
                Application.Restart();
            }
        }

        // --- 演出系 ---
        private void ShakeButton(Button b)
        {
            Point op = b.Location; int c = 0; Timer t = new Timer { Interval = 30 };
            t.Tick += (s, e) => { b.Location = new Point(op.X + (c++ % 2 == 0 ? 5 : -5), op.Y); if (c > 8) { t.Stop(); b.Location = op; } };
            t.Start();
        }

        private void ShowFloatingText(Button b, string txt)
        {
            Label l = new Label { Text = txt, ForeColor = Color.DarkGreen, Font = new Font("Arial", 14, FontStyle.Bold), AutoSize = true, Location = new Point(b.Parent.Left + b.Left, b.Parent.Top + b.Top) };
            this.Controls.Add(l); l.BringToFront();
            int d = 0; Timer t = new Timer { Interval = 30 };
            t.Tick += (s, e) => { l.Top -= 3; if (d++ > 20) { t.Stop(); this.Controls.Remove(l); } }; t.Start();
        }

        private void InitSound()
        {
            try { harvestSound = new SoundPlayer("se_harvest.wav"); bgmPlayer = new SoundPlayer("bgm_farm.wav"); bgmPlayer.PlayLooping(); }
            catch { }
        }

        private void SaveGame()
        {
            var s = new SaveData { Money = money, TotalEarned = totalEarned, Day = currentDay, Fertilizer = fertilizerCount, Pesticide = pesticideCount, Fields = farmField };
            File.WriteAllText("save.json", JsonSerializer.Serialize(s)); AddLog("保存しました。");
        }

        private void AddLog(string m) { txtLog.AppendText($"{m}{Environment.NewLine}"); txtLog.SelectionStart = txtLog.Text.Length; txtLog.ScrollToCaret(); }
    }
}
