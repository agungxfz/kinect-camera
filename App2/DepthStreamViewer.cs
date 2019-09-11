using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace App2
{
    class DepthStreamViewer
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private int frameRate;
        public int FrameRate
        {
            get { return frameRate; }
            set
            {
                frameRate = value;
                NotifyPropertyChanged("FrameRate");
            }
        }

        private int maxDepth;
        public int MaxDepth
        {
            get { return maxDepth; }
            set
            {
                maxDepth = value;
                NotifyPropertyChanged("MaxDepth");
            }
        }

        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this,
                    new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
