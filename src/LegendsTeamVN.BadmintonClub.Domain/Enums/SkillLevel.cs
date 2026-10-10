namespace LegendsTeamVN.BadmintonClub.Domain.Enums;

/// <summary>
/// Trình độ tự đánh giá của người dùng. Miền giá trị: 1=NB, 2=Y, 3=TBY, 4=TB, 5=TBK, 6=K.
/// </summary>
public enum SkillLevel : short
{
    /// <summary>1 = NB (Newbie / Mới chơi)</summary>
    NB = 1,

    /// <summary>2 = Y (Yếu)</summary>
    Y = 2,

    /// <summary>3 = TBY (Trung bình yếu)</summary>
    TBY = 3,

    /// <summary>4 = TB (Trung bình)</summary>
    TB = 4,

    /// <summary>5 = TBK (Trung bình khá)</summary>
    TBK = 5,

    /// <summary>6 = K (Khá)</summary>
    K = 6
}
