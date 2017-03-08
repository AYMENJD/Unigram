﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Converters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Messages
{
    public sealed partial class MessageStatusControl : HackedContentPresenter
    {
        public TLMessage ViewModel => DataContext as TLMessage;

        public BindConvert Convert => BindConvert.Current;

        private TLMessage _oldValue;

        public MessageStatusControl()
        {
            InitializeComponent();

            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null && ViewModel != _oldValue) Bindings.Update();
                if (ViewModel == null) Bindings.StopTracking();

                _oldValue = ViewModel;
            };
        }

        public string ConvertViews(TLMessage message, int? views)
        {
            var number = string.Empty;

            if (message.HasViews)
            {
                //ViewsGlyph.Text = "\uE607\u2009";
                ViewsGlyph.Text = "\uE607\u00A0\u00A0";

                number = Convert.ShortNumber(views ?? 0);
                number += "   ";

                if (message.IsPost && message.HasFromId && message.From != null)
                {
                    number += $"{message.From.FullName}, ";
                }
            }
            else
            {
                ViewsGlyph.Text = string.Empty;
            }

            return number;
        }

        private string ConvertEdit(bool hasEditDate, bool hasViaBotId, TLReplyMarkupBase replyMarkup)
        {
            return hasEditDate && !hasViaBotId && replyMarkup?.TypeId != TLType.ReplyInlineMarkup ? "edited\u00A0\u2009" : string.Empty;
        }

        private string ConvertState(bool isOut, TLMessageState value)
        {
            if (!isOut)
            {
                return string.Empty;
            }

            switch (value)
            {
                case TLMessageState.Sending:
                    return "\u00A0\u00A0\uE600";
                case TLMessageState.Confirmed:
                    return "\u00A0\u00A0\uE602";
                case TLMessageState.Read:
                    return "\u00A0\u00A0\uE601";
                default:
                    return "\u00A0\u00A0\uFFFD";
            }
        }
    }

    public class HackedContentPresenter : ContentPresenter
    {
        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;
    }
}
