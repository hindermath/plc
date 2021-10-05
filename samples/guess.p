var guess, num;
begin
    num := rand 1 10;
    while guess # num do {
        ? "Guess a number: " guess;
        if guess > num then ! "Lower";
        if guess < num then ! "Higher";
        if guess = num then ! "You nailed it!";
    }
end.
