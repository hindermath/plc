var x;
begin
    x := 1;
    begin
        if x = 1 then ! "I know x!";
        x := 2;
        if x = 2 then ! "Inside"
    end;
    if x = 2 then ! "and out!";
    ! x
end.
