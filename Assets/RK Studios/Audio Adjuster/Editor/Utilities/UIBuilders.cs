using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RK_Studios.Audio_Adjuster.Editor.Utilities {
    public static class UIBuilders {
        // Classes
        private const string ContainerClass = "row";
        private const string WidthClass = "width";
        private const string TopClass = "row-top";
        private const string TopBarClass = "row-top__bar";
        private const string TopBarInnerClass = "row-top__bar-inner";
        private const string BottomClass = "row-bottom";
        private const string BottomCellClass = "row-bottom__cell";
        private const string BottomCellFlexClass = "row-bottom__cell--flex";
        private const string BottomCellSmallClass = "row-bottom__cell--small";
        private const string BottomCellMediumClass = "row-bottom__cell--medium";
        private const string BottomCellNoSpaceClass = "row-bottom__cell--no-space";
        public const string BottomCellHiddenClass = "row-bottom__cell--hidden";
        private const string BottomButtonClass = "row-bottom__button";
        private const string BottomTitleClass = "row-bottom__title";
        private const string BottomTimeClass = "row-bottom__time";

        public static VisualElement Row(AudioClip clip, List<float> waveform) {
            // Wrapper
            var row = new VisualElement();
            row.AddToClassList(ContainerClass);
            var width = new VisualElement();
            width.AddToClassList(WidthClass);
            row.Add(width);

            // Waveform
            var top = new VisualElement();
            top.AddToClassList(TopClass);
            foreach (var h in waveform) {
                var bar = new VisualElement();
                bar.AddToClassList(TopBarClass);
                var inner = new VisualElement();
                inner.AddToClassList(TopBarInnerClass);
                inner.style.height = Length.Percent(h * 100f);
                bar.Add(inner);
                top.Add(bar);
            }

            width.Add(top);

            // Bottom container
            var bot = new VisualElement();
            bot.AddToClassList(BottomClass);

            // Title/time
            var cellFlex = new VisualElement();
            cellFlex.AddToClassList(BottomCellClass);
            cellFlex.AddToClassList(BottomCellFlexClass);
            var lblName = new Label(clip.name);
            lblName.AddToClassList(BottomTitleClass);
            var lblTime = new Label(clip.length.ToString("0.00") + "s");
            lblTime.AddToClassList(BottomTimeClass);
            cellFlex.Add(lblName);
            cellFlex.Add(lblTime);

            // Cells
            var cellVol = BuildVolumeAdjustor();
            var cellTrim = BuildIconButton("row-bottom__trim", "row-bottom__trim-btn");
            var cellUndo = BuildIconButton("row-bottom__undo", "row-bottom__undo-btn");
            cellUndo.AddToClassList(BottomCellHiddenClass);
            var cellPlay = BuildIconButton("row-bottom__play", "row-bottom__play-btn");
            cellPlay.AddToClassList(BottomCellNoSpaceClass);

            // Add everything
            bot.Add(cellFlex);
            bot.Add(cellVol);
            bot.Add(cellTrim);
            bot.Add(cellUndo);
            bot.Add(cellPlay);
            width.Add(bot);

            return row;
        }

        // Helpers
        private static VisualElement BuildIconButton(string iconCss, string btnCss) {
            var cell = new VisualElement();
            cell.AddToClassList(BottomCellClass);
            cell.AddToClassList(BottomCellSmallClass);

            var btn = new VisualElement();
            btn.AddToClassList(BottomButtonClass);
            btn.AddToClassList(btnCss);

            var icon = new VisualElement();
            icon.AddToClassList(iconCss);
            btn.Add(icon);
            cell.Add(btn);
            return cell;
        }

        private static VisualElement BuildVolumeAdjustor() {
            var cell = new VisualElement();
            cell.AddToClassList(BottomCellClass);
            cell.AddToClassList(BottomCellMediumClass);

            var root = new VisualElement();
            root.AddToClassList("volume-adjustor");

            /* minus ( LEFT side acts as button ) */
            var minus = new VisualElement();
            minus.AddToClassList("volume-adjustor__left");
            minus.AddToClassList("volume-adjustor__minus-btn");
            var minusIcon = new VisualElement();
            minusIcon.AddToClassList("volume-adjustor__minus");
            minus.Add(minusIcon);

            /* middle (bar + label) */
            var mid = new VisualElement();
            mid.AddToClassList("volume-adjustor__middle");
            var bar = new VisualElement();
            bar.AddToClassList("volume-adjustor__volume");
            var lbl = new Label("100%");
            lbl.AddToClassList("volume-adjustor__label");
            mid.Add(bar);
            mid.Add(lbl);

            /* plus ( RIGHT side acts as button ) */
            var plus = new VisualElement();
            plus.AddToClassList("volume-adjustor__right");
            plus.AddToClassList("volume-adjustor__plus-btn");
            var plusIcon = new VisualElement();
            plusIcon.AddToClassList("volume-adjustor__plus");
            plus.Add(plusIcon);

            /* build tree */
            root.Add(minus);
            root.Add(mid);
            root.Add(plus);
            cell.Add(root);
            return cell;
        }
    }
}