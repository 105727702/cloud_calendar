using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MyAvaloniaApp.ViewModels
{
    public class CalendarManager : INotifyPropertyChanged
    {
        private DateTime _currentWeekStart = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek);

        #region Properties

        public DateTime GetCurrentWeekStart() => _currentWeekStart;

        public string CurrentWeekDisplay
        {
            get
            {
                var endWeek = _currentWeekStart.AddDays(6);
                return $"{_currentWeekStart:dd/MM/yyyy} - {endWeek:dd/MM/yyyy}";
            }
        }

        public string SundayDate => _currentWeekStart.Day.ToString();
        public string MondayDate => _currentWeekStart.AddDays(1).Day.ToString();
        public string TuesdayDate => _currentWeekStart.AddDays(2).Day.ToString();
        public string WednesdayDate => _currentWeekStart.AddDays(3).Day.ToString();
        public string ThursdayDate => _currentWeekStart.AddDays(4).Day.ToString();
        public string FridayDate => _currentWeekStart.AddDays(5).Day.ToString();
        public string SaturdayDate => _currentWeekStart.AddDays(6).Day.ToString();

        // Today highlighting properties
        public bool IsSundayToday => DateTime.Today == _currentWeekStart.Date;
        public bool IsMondayToday => DateTime.Today == _currentWeekStart.AddDays(1).Date;
        public bool IsTuesdayToday => DateTime.Today == _currentWeekStart.AddDays(2).Date;
        public bool IsWednesdayToday => DateTime.Today == _currentWeekStart.AddDays(3).Date;
        public bool IsThursdayToday => DateTime.Today == _currentWeekStart.AddDays(4).Date;
        public bool IsFridayToday => DateTime.Today == _currentWeekStart.AddDays(5).Date;
        public bool IsSaturdayToday => DateTime.Today == _currentWeekStart.AddDays(6).Date;

        #endregion

        #region Methods

        public void PreviousWeek()
        {
            _currentWeekStart = _currentWeekStart.AddDays(-7);
            RefreshCalendarProperties();
        }

        public void NextWeek()
        {
            _currentWeekStart = _currentWeekStart.AddDays(7);
            RefreshCalendarProperties();
        }

        private void RefreshCalendarProperties()
        {
            OnPropertyChanged(nameof(CurrentWeekDisplay));
            OnPropertyChanged(nameof(SundayDate));
            OnPropertyChanged(nameof(MondayDate));
            OnPropertyChanged(nameof(TuesdayDate));
            OnPropertyChanged(nameof(WednesdayDate));
            OnPropertyChanged(nameof(ThursdayDate));
            OnPropertyChanged(nameof(FridayDate));
            OnPropertyChanged(nameof(SaturdayDate));
            OnPropertyChanged(nameof(IsSundayToday));
            OnPropertyChanged(nameof(IsMondayToday));
            OnPropertyChanged(nameof(IsTuesdayToday));
            OnPropertyChanged(nameof(IsWednesdayToday));
            OnPropertyChanged(nameof(IsThursdayToday));
            OnPropertyChanged(nameof(IsFridayToday));
            OnPropertyChanged(nameof(IsSaturdayToday));
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
