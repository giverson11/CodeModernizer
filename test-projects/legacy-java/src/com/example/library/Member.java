package com.example.library;

/**
 * Membership hierarchy modeled with an int "kind" tag and instanceof chains
 * elsewhere - a candidate for sealed types and pattern matching.
 */
public abstract sealed class Member permits Member.Student, Member.Staff, Member.Guest {

    public static final int KIND_STUDENT = 0;
    public static final int KIND_STAFF = 1;
    public static final int KIND_GUEST = 2;

    private final String name;
    private final int kind;

    protected Member(String name, int kind) {
        this.name = name;
        this.kind = kind;
    }

    public String getName() {
        return name;
    }

    public int getKind() {
        return kind;
    }

    public abstract int maxLoans();

    public static final class Student extends Member {
        private final String school;

        public Student(String name, String school) {
            super(name, KIND_STUDENT);
            this.school = school;
        }

        public String getSchool() {
            return school;
        }

        @Override
        public int maxLoans() {
            return 5;
        }
    }

    public static final class Staff extends Member {
        public Staff(String name) {
            super(name, KIND_STAFF);
        }

        @Override
        public int maxLoans() {
            return 10;
        }
    }

    public static final class Guest extends Member {
        public Guest(String name) {
            super(name, KIND_GUEST);
        }

        @Override
        public int maxLoans() {
            return 1;
        }
    }
}
